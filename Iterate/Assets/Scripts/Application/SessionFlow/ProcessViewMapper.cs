using System.Collections.Generic;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Copies the Process in play into a <see cref="ProcessView"/>. Pending arrivals are counted, never
    /// named.
    /// </summary>
    internal static class ProcessViewMapper
    {
        /// <summary>
        /// Maps the context's Process.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The view, or null outside a Process.</returns>
        internal static ProcessView Map(SessionContext context)
        {
            ProcessState process = context.Process;
            if (process == null)
                return null;

            InstructionBuffer buffer = process.Buffer;
            IReadOnlyList<BufferSlot> slots = buffer.Slots;
            List<BufferSlotView> slotViews = new(slots.Count);
            for (int index = 0; index < slots.Count; index++)
            {
                RepositoryItem item = slots[index].Item;
                slotViews.Add(new BufferSlotView(slots[index].Number, item?.InstanceID, item?.DefinitionID));
            }

            ProcessCounters counters = process.Counters;
            return new ProcessView(
                process.ProcessIdentity,
                slotViews,
                buffer.IncomingItem?.InstanceID,
                buffer.PendingArrivals.Count,
                IdentitiesOf(buffer.ArchivedItems),
                IdentitiesOf(buffer.ConsumedItems),
                new List<InstanceID>(process.ExposedInstanceIDs),
                process.Bytes.Balance.Value,
                counters.ExecutionsRun,
                counters.ExecutionsRemaining,
                counters.MandatoryExecutionsRemaining
            );
        }

        /// <summary>
        /// Copies the identities of a list of items.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <returns>Their identities, in order.</returns>
        private static List<InstanceID> IdentitiesOf(IReadOnlyList<RepositoryItem> items)
        {
            List<InstanceID> identities = new(items.Count);
            for (int index = 0; index < items.Count; index++)
                identities.Add(items[index].InstanceID);

            return identities;
        }
    }
}