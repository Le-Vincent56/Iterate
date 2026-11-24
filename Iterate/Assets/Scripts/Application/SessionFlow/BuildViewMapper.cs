using System.Collections.Generic;
using Iterate.Domain.Compilation;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Copies the Build into a <see cref="BuildView"/>, with the status computed against the Process's
    /// current Bytes and compilation effects.
    /// </summary>
    internal static class BuildViewMapper
    {
        /// <summary>
        /// Maps the context's Build.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The view, or null outside the Build stage.</returns>
        internal static BuildView Map(SessionContext context)
        {
            BuildState build = context.Build;
            if (build == null || context.Stage != SessionStage.Build)
                return null;

            IReadOnlyList<SourceSlot> slots = build.CurrentArrangement.Slots;
            List<SourceSlotView> slotViews = new(slots.Count);
            for (int index = 0; index < slots.Count; index++)
                slotViews.Add(ViewOf(slots[index]));

            BuildStatus status = build.GetStatus(context.Process.Bytes.Balance, context.Process.CompilationEffects);
            return new BuildView(slotViews, status, build.PendingPragmas.Count);
        }

        /// <summary>
        /// Copies one source line.
        /// </summary>
        /// <param name="slot">The line.</param>
        /// <returns>The line's view.</returns>
        private static SourceSlotView ViewOf(SourceSlot slot)
        {
            if (slot.Instruction != null)
                return new SourceSlotView(slot.Position.LineNumber, slot.Kind, slot.Instruction.InstanceID, slot.Instruction.Definition.ID.Value);

            if (slot.Structure != null)
                return new SourceSlotView(slot.Position.LineNumber, slot.Kind, slot.Structure.InstanceID, slot.Structure.Definition.ID.Value);

            return new SourceSlotView(slot.Position.LineNumber, slot.Kind, null, null);
        }
    }
}