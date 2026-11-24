using System;
using System.Collections.Generic;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Values;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// The Instruction Buffer's state for telemetry: capacity, slots, the incoming item, the items
    /// exposed in arrival order, and the arrivals still pending. Equality compares the lists element
    /// by element, so an unchanged Buffer is not written twice.
    /// </summary>
    public sealed record BufferSnapshot
    {
        /// <summary>
        /// The Buffer's capacity.
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// The Buffer's slots, in slot order.
        /// </summary>
        public IReadOnlyList<BufferSlotView> Slots { get; }

        /// <summary>
        /// The item waiting to enter a full Buffer, or null.
        /// </summary>
        public InstanceID? Incoming { get; }

        /// <summary>
        /// The items exposed to the Buffer, in arrival order.
        /// </summary>
        public IReadOnlyList<InstanceID> Exposed { get; }

        /// <summary>
        /// The arrivals still pending.
        /// </summary>
        public int PendingArrivals { get; }

        /// <summary>
        /// Creates a snapshot.
        /// </summary>
        /// <param name="capacity">The Buffer's capacity.</param>
        /// <param name="slots">The Buffer's slots.</param>
        /// <param name="incoming">The incoming item, or null.</param>
        /// <param name="exposed">The exposed items, in arrival order.</param>
        /// <param name="pendingArrivals">The arrivals still pending.</param>
        /// <exception cref="ArgumentNullException">Thrown when a list is null.</exception>
        public BufferSnapshot(
            int capacity,
            IReadOnlyList<BufferSlotView> slots,
            InstanceID? incoming,
            IReadOnlyList<InstanceID> exposed,
            int pendingArrivals
        )
        {
            Capacity = capacity;
            Slots = slots ?? throw new ArgumentNullException(nameof(slots));
            Incoming = incoming;
            Exposed = exposed ?? throw new ArgumentNullException(nameof(exposed));
            PendingArrivals = pendingArrivals;
        }

        /// <summary>
        /// Compares every member, the lists element by element.
        /// </summary>
        /// <param name="other">The snapshot to compare with.</param>
        /// <returns>Whether the two describe the same Buffer state.</returns>
        public bool Equals(BufferSnapshot other)
        {
            if (other == null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Capacity != other.Capacity || Incoming != other.Incoming || PendingArrivals != other.PendingArrivals)
                return false;

            if (Slots.Count != other.Slots.Count || Exposed.Count != other.Exposed.Count)
                return false;

            for (int index = 0; index < Slots.Count; index++)
            {
                if (!Slots[index].Equals(other.Slots[index]))
                    return false;
            }

            for (int index = 0; index < Exposed.Count; index++)
            {
                if (Exposed[index] != other.Exposed[index])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Hashes every member, the lists element by element.
        /// </summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(Capacity);
            hash.Add(Incoming);
            hash.Add(PendingArrivals);
            for (int index = 0; index < Slots.Count; index++)
                hash.Add(Slots[index]);

            for (int index = 0; index < Exposed.Count; index++)
                hash.Add(Exposed[index]);

            return hash.ToHashCode();
        }
    }
}