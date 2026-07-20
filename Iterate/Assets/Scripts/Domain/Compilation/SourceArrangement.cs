using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// An immutable source arrangement: the full ordered set of numbered slots. Construction validates
    /// contiguous one-based numbering, Structure footprint and ownership (each header followed by exactly
    /// footprint − 1 contained slots owned by the same instance), no duplicate Structure, and no nesting.
    /// Updates are functional (<see cref="WithSlots"/>) — the original is never mutated.
    /// </summary>
    public sealed class SourceArrangement
    {
        private readonly SourceSlot[] _slots;

        /// <summary>
        /// The slots in position order.
        /// </summary>
        public IReadOnlyList<SourceSlot> Slots => _slots;

        /// <summary>
        /// The number of slots in the arrangement.
        /// </summary>
        public int SlotCount => _slots.Length;

        public SourceArrangement(IReadOnlyList<SourceSlot> slots)
        {
            _slots = Validate(slots);
        }

        /// <summary>
        /// Returns the slot occupying a position.
        /// </summary>
        /// <param name="position">The position to look up.</param>
        /// <returns>The slot at the position.</returns>
        /// <exception cref="ArgumentException">Thrown when no slot occupies the position.</exception>
        public SourceSlot SlotAt(SourcePosition position)
        {
            int index = position.LineNumber - 1;
            if (index < 0 || index >= _slots.Length)
                throw new ArgumentException($"No slot occupies position {position.LineNumber}.", nameof(position));

            return _slots[index];
        }

        /// <summary>
        /// Finds the slot that introduces an instance — the Instruction occupant carrying the identity,
        /// or the Structure header owning it.
        /// </summary>
        /// <param name="instanceID">The instance identity to find.</param>
        /// <param name="slot">The matching slot when found; null otherwise.</param>
        /// <returns>True when a matching slot exists; false otherwise.</returns>
        public bool TryFindInstance(InstanceID instanceID, out SourceSlot slot)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                SourceSlot candidate = _slots[i];
                if (candidate.Instruction != null && candidate.Instruction.InstanceID == instanceID)
                {
                    slot = candidate;
                    return true;
                }

                if (candidate.Kind == SourceSlotKind.StructureHeader && candidate.Structure.InstanceID == instanceID)
                {
                    slot = candidate;
                    return true;
                }
            }

            slot = null;
            return false;
        }

        /// <summary>
        /// Produces a new arrangement with the given slots replaced by position, revalidated. The
        /// original arrangement is left untouched.
        /// </summary>
        /// <param name="replacements">The slots to overlay, keyed by their position.</param>
        /// <returns>A new, revalidated arrangement.</returns>
        /// <exception cref="ArgumentException">Thrown when a replacement position is out of range or the result is invalid.</exception>
        public SourceArrangement WithSlots(IReadOnlyList<SourceSlot> replacements)
        {
            if (replacements == null)
                throw new ArgumentException("A replacement set is required.", nameof(replacements));

            SourceSlot[] next = new SourceSlot[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
                next[i] = _slots[i];

            for (int i = 0; i < replacements.Count; i++)
            {
                SourceSlot replacement = replacements[i];
                if (replacement == null)
                    throw new ArgumentException("A replacement slot must not be null.", nameof(replacements));

                int index = replacement.Position.LineNumber - 1;
                if (index < 0 || index >= next.Length)
                    throw new ArgumentException($"Replacement position {replacement.Position.LineNumber} is outside the arrangement.", nameof(replacements));

                next[index] = replacement;
            }

            return new SourceArrangement(next);
        }

        /// <summary>
        /// Validates and copies the slot list into a defensive array.
        /// </summary>
        /// <param name="slots">The candidate slot list.</param>
        /// <returns>The validated slots as a private array.</returns>
        /// <exception cref="ArgumentException">Thrown when numbering, Structure spans, ownership, or uniqueness are violated.</exception>
        private static SourceSlot[] Validate(IReadOnlyList<SourceSlot> slots)
        {
            if (slots == null)
                throw new ArgumentException("A SourceArrangement requires a slot list.", nameof(slots));

            if (slots.Count == 0)
                throw new ArgumentException("A SourceArrangement requires at least one slot.", nameof(slots));

            SourceSlot[] copy = new SourceSlot[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                SourceSlot slot = slots[i];
                if (slot == null)
                    throw new ArgumentException("A SourceArrangement slot must not be null.", nameof(slots));

                if (slot.Position.LineNumber != i + 1)
                    throw new ArgumentException($"Slot at index {i} must occupy position {i + 1}, but occupies {slot.Position.LineNumber}.", nameof(slots));

                copy[i] = slot;
            }

            ValidateStructures(copy);
            return copy;
        }

        /// <summary>
        /// Validates Structure footprint, contained ownership, no-nesting, and no-duplicate rules.
        /// </summary>
        /// <param name="slots">The position-ordered slots.</param>
        /// <exception cref="ArgumentException">Thrown when a Structure span, ownership, or uniqueness rule is violated.</exception>
        private static void ValidateStructures(SourceSlot[] slots)
        {
            HashSet<int> seenStructures = new HashSet<int>();
            int index = 0;
            while (index < slots.Length)
            {
                SourceSlot slot = slots[index];
                if (slot.Kind == SourceSlotKind.ContainedEmpty || slot.Kind == SourceSlotKind.ContainedInstruction)
                    throw new ArgumentException($"Contained slot at position {slot.Position.LineNumber} has no owning Structure header.", nameof(slots));

                if (slot.Kind != SourceSlotKind.StructureHeader)
                {
                    index++;
                    continue;
                }

                StructureInstance structure = slot.Structure;
                if (!seenStructures.Add(structure.InstanceID.Value))
                    throw new ArgumentException($"Structure {structure.InstanceID} appears more than once (position {slot.Position.LineNumber}).", nameof(slots));

                int footprint = structure.Definition.SourceFootprint;
                for (int offset = 1; offset < footprint; offset++)
                {
                    int containedIndex = index + offset;
                    if (containedIndex >= slots.Length)
                        throw new ArgumentException($"Structure header at position {slot.Position.LineNumber} runs past the end of the arrangement.", nameof(slots));

                    SourceSlot contained = slots[containedIndex];
                    if (contained.Kind != SourceSlotKind.ContainedEmpty && contained.Kind != SourceSlotKind.ContainedInstruction)
                        throw new ArgumentException($"Structure header at position {slot.Position.LineNumber} must be followed by contained slots.", nameof(slots));

                    if (contained.Structure.InstanceID != structure.InstanceID)
                        throw new ArgumentException($"Contained slot at position {contained.Position.LineNumber} is not owned by the Structure at position {slot.Position.LineNumber}.", nameof(slots));
                }

                index += footprint;
            }
        }
    }
}