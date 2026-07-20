using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// Removes an installed Instruction (top-level or contained) or an empty whole Structure back to the
    /// buffer, requiring one free Buffer slot. A removed Instruction's origin becomes empty; a removed
    /// Structure's span reverts to top-level empties. Removing a Structure that still holds Instructions
    /// is rejected, since removal must produce exactly one incoming Buffer item.
    /// </summary>
    public sealed class RemoveEdit : ISourceEdit
    {
        private readonly SourcePosition _origin;

        /// <inheritdoc />
        public bool IsFreeOnly => false;

        /// <summary>
        /// Creates a remove edit.
        /// </summary>
        /// <param name="origin">The occupied origin position.</param>
        public RemoveEdit(SourcePosition origin)
        {
            _origin = origin;
        }

        /// <inheritdoc />
        public BuildOperationResult TryApply(SourceArrangement current, IBuildBuffer buffer, out SourceEditOutcome outcome)
        {
            outcome = null;
            if (_origin.LineNumber > current.SlotCount)
                return BuildOperationResult.Rejected(SourceEditRejection.PositionNotFound);

            SourceSlot slot = current.SlotAt(_origin);
            switch (slot.Kind)
            {
                case SourceSlotKind.Core:
                    return BuildOperationResult.Rejected(SourceEditRejection.CoreImmutable);
                
                case SourceSlotKind.Empty:
                case SourceSlotKind.ContainedEmpty:
                    return BuildOperationResult.Rejected(SourceEditRejection.OriginEmpty);
                
                case SourceSlotKind.Instruction:
                    return RemoveInstruction(current, buffer, slot, true, out outcome);
                
                case SourceSlotKind.ContainedInstruction:
                    return RemoveInstruction(current, buffer, slot, false, out outcome);
                
                case SourceSlotKind.StructureHeader:
                    return RemoveStructure(current, buffer, slot, out outcome);
                
                default:
                    return BuildOperationResult.Rejected(SourceEditRejection.OriginEmpty);
            }
        }

        /// <summary>
        /// Removes an Instruction back to the buffer, vacating its origin.
        /// </summary>
        /// <param name="current">The arrangement to update.</param>
        /// <param name="buffer">The buffer to accept the removed Instruction.</param>
        /// <param name="slot">The Instruction origin slot.</param>
        /// <param name="isTopLevel">Whether the origin is a top-level slot.</param>
        /// <param name="outcome">The produced outcome on success; null when rejected.</param>
        /// <returns>Success, or a typed rejection.</returns>
        private BuildOperationResult RemoveInstruction(
            SourceArrangement current,
            IBuildBuffer buffer,
            SourceSlot slot,
            bool isTopLevel,
            out SourceEditOutcome outcome
        )
        {
            outcome = null;
            if (!buffer.HasRemovalCapacity)
                return BuildOperationResult.Rejected(SourceEditRejection.BufferFull);

            buffer.AcceptRemoved(slot.Instruction);
            SourceSlot vacated = isTopLevel
                ? SourceSlot.ForEmpty(_origin)
                : SourceSlot.ForContainedEmpty(_origin, slot.Structure);

            List<SourceSlot> replacements = new List<SourceSlot> { vacated };
            outcome = new SourceEditOutcome(current.WithSlots(replacements), Array.Empty<InstanceID>());
            return BuildOperationResult.Success;
        }

        /// <summary>
        /// Removes an empty whole Structure back to the buffer, reverting its span to top-level empties.
        /// </summary>
        /// <param name="current">The arrangement to update.</param>
        /// <param name="buffer">The buffer to accept the removed Structure.</param>
        /// <param name="headerSlot">The Structure header origin slot.</param>
        /// <param name="outcome">The produced outcome on success; null when rejected.</param>
        /// <returns>Success, or a typed rejection.</returns>
        private BuildOperationResult RemoveStructure(
            SourceArrangement current,
            IBuildBuffer buffer,
            SourceSlot headerSlot,
            out SourceEditOutcome outcome
        )
        {
            outcome = null;
            StructureInstance structure = headerSlot.Structure;
            int footprint = structure.Definition.SourceFootprint;
            for (int offset = 1; offset < footprint; offset++)
            {
                SourceSlot contained = current.SlotAt(new SourcePosition(_origin.LineNumber + offset));
                if (contained.Kind == SourceSlotKind.ContainedInstruction)
                    return BuildOperationResult.Rejected(SourceEditRejection.InvalidContainment);
            }

            if (!buffer.HasRemovalCapacity)
                return BuildOperationResult.Rejected(SourceEditRejection.BufferFull);

            buffer.AcceptRemoved(structure);
            List<SourceSlot> replacements = new List<SourceSlot>();
            for (int offset = 0; offset < footprint; offset++)
                replacements.Add(SourceSlot.ForEmpty(new SourcePosition(_origin.LineNumber + offset)));

            outcome = new SourceEditOutcome(current.WithSlots(replacements), Array.Empty<InstanceID>());
            return BuildOperationResult.Success;
        }
    }
}