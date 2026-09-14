using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The Process-scoped Instruction Buffer, and Compilation's <see cref="IBuildBuffer"/> seam. Slots
    /// are numbered one to capacity for the Process's whole life; an item keeps its slot until it
    /// leaves, and anything entering takes the lowest open slot. An item arriving to a full Buffer is
    /// held outside it — not refused — beginning a blocking overflow state that the player resolves by
    /// installing a buffered item, consuming a Directive, archiving a buffered item, or archiving the
    /// incoming one. The moment any of those frees a slot, the queue head is admitted inside the same
    /// call: that drain validates nothing and can neither reject nor throw, which is what makes it safe
    /// for it to run inside Compilation's own edit path.
    /// </summary>
    public sealed class InstructionBuffer : IBuildBuffer
    {
        private readonly RepositoryItem[] _slots;
        private readonly List<RepositoryItem> _queue = new();
        private readonly List<RepositoryItem> _archived = new();
        private readonly List<RepositoryItem> _consumed = new();
        private readonly List<BufferRecord> _records = new();
        private readonly Dictionary<InstanceID, BufferItemState> _states = new();

        /// <summary>
        /// The number of slots this Buffer carries for the Process's whole life.
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// The slots in number order; a slot's item is null when it is open.
        /// </summary>
        public IReadOnlyList<BufferSlot> Slots
        {
            get
            {
                List<BufferSlot> slots = new(_slots.Length);
                for (int index = 0; index < _slots.Length; index++)
                {
                    slots.Add(new BufferSlot(index + 1, _slots[index]));
                }

                return slots;
            }
        }

        /// <summary>
        /// Whether an item is being held outside a full Buffer.
        /// </summary>
        public bool IsOverflowing => _queue.Count > 0;

        /// <summary>
        /// The item held outside the Buffer, or null when there is none.
        /// </summary>
        public RepositoryItem IncomingItem => _queue.Count > 0 ? _queue[0] : null;

        /// <summary>
        /// The items queued behind the incoming one, in arrival order.
        /// </summary>
        public IReadOnlyList<RepositoryItem> PendingArrivals
        {
            get
            {
                List<RepositoryItem> pending = new();
                for (int index = 1; index < _queue.Count; index++)
                {
                    pending.Add(_queue[index]);
                }

                return pending;
            }
        }

        /// <summary>
        /// The items archived for the rest of this Process, in archive order.
        /// </summary>
        public IReadOnlyList<RepositoryItem> ArchivedItems => _archived;

        /// <summary>
        /// The Directives consumed in this Process, in consumption order.
        /// </summary>
        public IReadOnlyList<RepositoryItem> ConsumedItems => _consumed;

        /// <summary>
        /// The Buffer transaction records in the order they occurred.
        /// </summary>
        public IReadOnlyList<BufferRecord> Records => _records;

        /// <inheritdoc />
        public bool HasRemovalCapacity => FindOpenSlot() >= 0;

        public InstructionBuffer(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentException("An Instruction Buffer requires at least one slot.", nameof(capacity));

            Capacity = capacity;
            _slots = new RepositoryItem[capacity];
        }

        /// <summary>
        /// Offers one item to the Buffer. It takes the lowest open slot, or is held outside a full
        /// Buffer, beginning or extending an overflow state.
        /// </summary>
        /// <param name="item">The item to admit.</param>
        /// <returns>The admission result.</returns>
        public AdmissionResult Admit(RepositoryItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (IsOverflowing)
                return Hold(item);

            int slot = FindOpenSlot();
            if (slot < 0)
                return Hold(item);

            return Place(item, slot);
        }

        /// <summary>
        /// Archives one buffered item: unavailable for the rest of this Process, returned to the
        /// Repository afterward. Freeing its slot drains one queued arrival in this same call.
        /// </summary>
        /// <param name="id">The buffered instance to archive.</param>
        /// <returns>The archive result.</returns>
        /// <remarks>
        /// Archive through <see cref="ProcessState.Archive"/> rather than here: this method commits the
        /// archive but does not fire the Dependencies that observe one.
        /// </remarks>
        public ArchiveResult Archive(InstanceID id)
        {
            int slot = FindSlotOf(id);
            if (slot < 0)
                return ArchiveResult.Rejected(ArchiveRejection.UnknownInstance);

            RepositoryItem item = _slots[slot];
            _slots[slot] = null;
            _archived.Add(item);
            _states[id] = BufferItemState.Archived;
            _records.Add(new ArchiveRecord(id, item.DefinitionID, false));
            Drain();
            return ArchiveResult.Success(item);
        }

        /// <summary>
        /// Archives the item held outside a full Buffer. It never enters the Buffer, and the next
        /// queued arrival becomes the incoming one.
        /// </summary>
        /// <returns>The archive result.</returns>
        /// <remarks>
        /// Archive through <see cref="ProcessState.ArchiveIncoming"/> rather than here, for the same
        /// reason as <see cref="Archive"/>.
        /// </remarks>
        public ArchiveResult ArchiveIncoming()
        {
            if (!IsOverflowing)
                return ArchiveResult.Rejected(ArchiveRejection.NothingIncoming);

            RepositoryItem item = _queue[0];
            _queue.RemoveAt(0);
            _archived.Add(item);
            _states[item.InstanceID] = BufferItemState.Archived;
            _records.Add(new ArchiveRecord(item.InstanceID, item.DefinitionID, true));
            Drain();
            return ArchiveResult.Success(item);
        }

        /// <summary>
        /// What became of an item this Buffer has held.
        /// </summary>
        /// <param name="id">The instance identity.</param>
        /// <returns>The item's state; None when the Buffer has never held it.</returns>
        public BufferItemState StateOf(InstanceID id)
        {
            return _states.TryGetValue(id, out BufferItemState state) ? state : BufferItemState.None;
        }

        /// <inheritdoc />
        public bool TryPeekInstruction(InstanceID instanceID, out InstructionInstance instance)
        {
            int slot = FindSlotOf(instanceID);
            if (slot >= 0 && _slots[slot].Kind == RepositoryItemKind.Instruction)
            {
                instance = _slots[slot].Instruction;
                return true;
            }

            instance = null;
            return false;
        }

        /// <inheritdoc />
        public bool TryPeekStructure(InstanceID instanceID, out StructureInstance instance)
        {
            int slot = FindSlotOf(instanceID);
            if (slot >= 0 && _slots[slot].Kind == RepositoryItemKind.Structure)
            {
                instance = _slots[slot].Structure;
                return true;
            }

            instance = null;
            return false;
        }

        /// <inheritdoc />
        public bool TryPeekDirective(InstanceID instanceID, out DirectiveInstance instance)
        {
            int slot = FindSlotOf(instanceID);
            if (slot >= 0 && _slots[slot].Kind == RepositoryItemKind.Directive)
            {
                instance = _slots[slot].Directive;
                return true;
            }

            instance = null;
            return false;
        }

        /// <inheritdoc />
        public void Take(InstanceID instanceID)
        {
            int slot = FindSlotOf(instanceID);
            if (slot < 0)
                throw new InvalidOperationException("The Buffer holds no item with identity " + instanceID + " to take.");

            RepositoryItem item = _slots[slot];
            _slots[slot] = null;

            if (item.Kind == RepositoryItemKind.Directive)
            {
                _consumed.Add(item);
                _states[instanceID] = BufferItemState.Consumed;
                _records.Add(new ConsumeRecord(instanceID, item.DefinitionID));
            }
            else
            {
                _states.Remove(instanceID);
            }

            Drain();
        }

        /// <inheritdoc />
        public void AcceptRemoved(InstructionInstance removed)
        {
            AcceptRemovedItem(new RepositoryItem(
                RepositoryItemKind.Instruction,
                removed.InstanceID,
                removed.Definition.ID.Value,
                removed.Definition.Tags,
                removed,
                null,
                null)
            );
        }

        /// <inheritdoc />
        public void AcceptRemoved(StructureInstance removed)
        {
            AcceptRemovedItem(RepositoryItem.From(removed.Definition, removed.InstanceID));
        }

        /// <summary>
        /// Returns a removed source item to the lowest open slot. The caller guards with
        /// <see cref="HasRemovalCapacity"/>; reaching here without room is a contract violation.
        /// </summary>
        /// <param name="item">The item being returned.</param>
        /// <exception cref="InvalidOperationException">Thrown when the Buffer is full.</exception>
        private void AcceptRemovedItem(RepositoryItem item)
        {
            int slot = FindOpenSlot();
            if (slot < 0)
                throw new InvalidOperationException("The Buffer has no room to accept a removed item.");

            Place(item, slot);
        }

        /// <summary>
        /// Admits queued arrivals while slots are free, one item per freed slot. Validates nothing and
        /// cannot fail, so an edit that frees a slot never leaves the Buffer half-resolved.
        /// </summary>
        private void Drain()
        {
            while (_queue.Count > 0)
            {
                int slot = FindOpenSlot();
                if (slot < 0)
                    return;

                RepositoryItem item = _queue[0];
                _queue.RemoveAt(0);
                Place(item, slot);
            }
        }

        /// <summary>
        /// Places an item in a slot and records the admission.
        /// </summary>
        /// <param name="item">The item to place.</param>
        /// <param name="slot">The zero-based slot index.</param>
        /// <returns>The admission result.</returns>
        private AdmissionResult Place(RepositoryItem item, int slot)
        {
            _slots[slot] = item;
            _states[item.InstanceID] = BufferItemState.Present;
            _records.Add(new AdmissionRecord(item.InstanceID, item.DefinitionID, slot + 1));
            return AdmissionResult.Admitted(slot + 1, item);
        }

        /// <summary>
        /// Holds an item outside a full Buffer and records the overflow.
        /// </summary>
        /// <param name="item">The item to hold.</param>
        /// <returns>The admission result.</returns>
        private AdmissionResult Hold(RepositoryItem item)
        {
            _queue.Add(item);
            _records.Add(new OverflowRecord(item.InstanceID, item.DefinitionID, _queue.Count));
            return AdmissionResult.Held(item);
        }

        /// <summary>
        /// Finds the lowest open slot.
        /// </summary>
        /// <returns>The zero-based index of the lowest open slot, or -1 when every slot is full.</returns>
        private int FindOpenSlot()
        {
            for (int index = 0; index < _slots.Length; index++)
            {
                if (_slots[index] == null)
                    return index;
            }

            return -1;
        }

        /// <summary>
        /// Finds the slot holding an instance. The held incoming item is deliberately not found: it is
        /// outside the Buffer until a slot frees.
        /// </summary>
        /// <param name="id">The instance identity.</param>
        /// <returns>The zero-based slot index, or -1 when no slot holds it.</returns>
        private int FindSlotOf(InstanceID id)
        {
            for (int index = 0; index < _slots.Length; index++)
            {
                if (_slots[index] != null && _slots[index].InstanceID == id)
                    return index;
            }

            return -1;
        }
    }
}