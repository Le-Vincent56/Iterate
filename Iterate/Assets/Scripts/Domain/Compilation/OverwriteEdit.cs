using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The free-only Overwrite edit: a buffered Instruction tagged "Overwrite" replaces an occupied player
    /// object. An Instruction target archives its occupant and takes the slot; a Structure header target
    /// unwrap: the Structure is archived, the Overwrite takes the former header line, each
    /// contained Instruction becomes a top-level Instruction at its position retaining identity and Patches,
    /// and each contained empty becomes a top-level empty. Total slot count is unchanged.
    /// </summary>
    public sealed class OverwriteEdit : ISourceEdit
    {
        private const string OverwriteTag = "Overwrite";

        private readonly InstanceID _overwriteItem;
        private readonly SourcePosition _target;

        /// <inheritdoc />
        public bool IsFreeOnly => true;

        /// <summary>
        /// Creates an Overwrite edit.
        /// </summary>
        /// <param name="overwriteItem">The identity of the buffered Overwrite Instruction.</param>
        /// <param name="target">The occupied player target position.</param>
        public OverwriteEdit(InstanceID overwriteItem, SourcePosition target)
        {
            _overwriteItem = overwriteItem;
            _target = target;
        }

        /// <inheritdoc />
        public BuildOperationResult TryApply(SourceArrangement current, IBuildBuffer buffer, out SourceEditOutcome outcome)
        {
            outcome = null;
            if (!buffer.TryPeekInstruction(_overwriteItem, out InstructionInstance overwrite) || !HasOverwriteTag(overwrite))
                return BuildOperationResult.Rejected(SourceEditRejection.NotAnOverwriteInstruction);

            if (_target.LineNumber > current.SlotCount)
                return BuildOperationResult.Rejected(SourceEditRejection.PositionNotFound);

            SourceSlot targetSlot = current.SlotAt(_target);
            switch (targetSlot.Kind)
            {
                case SourceSlotKind.Core:
                    return BuildOperationResult.Rejected(SourceEditRejection.CoreImmutable);
                case SourceSlotKind.Empty:
                case SourceSlotKind.ContainedEmpty:
                    return BuildOperationResult.Rejected(SourceEditRejection.OverwriteTargetEmpty);
                case SourceSlotKind.Instruction:
                    return OverwriteOccupant(current, buffer, targetSlot, SourceSlot.ForInstruction(_target, overwrite), out outcome);
                case SourceSlotKind.ContainedInstruction:
                    return OverwriteOccupant(current, buffer, targetSlot, SourceSlot.ForContainedInstruction(_target, targetSlot.Structure, overwrite), out outcome);
                case SourceSlotKind.StructureHeader:
                    return OverwriteStructure(current, buffer, targetSlot, overwrite, out outcome);
                default:
                    return BuildOperationResult.Rejected(SourceEditRejection.OverwriteTargetIneligible);
            }
        }

        /// <summary>
        /// Whether an Instruction's definition carries the Overwrite tag.
        /// </summary>
        /// <param name="instruction">The buffered Instruction.</param>
        /// <returns>True when the definition tags contain the Overwrite tag.</returns>
        private bool HasOverwriteTag(InstructionInstance instruction)
        {
            IReadOnlyList<string> tags = instruction.Definition.Tags;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == OverwriteTag)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Replaces an occupied Instruction slot with the Overwrite, archiving the former occupant.
        /// </summary>
        /// <param name="current">The arrangement to update.</param>
        /// <param name="buffer">The buffer to take the Overwrite from.</param>
        /// <param name="targetSlot">The occupied target slot.</param>
        /// <param name="replacement">The Overwrite-occupied replacement slot.</param>
        /// <param name="outcome">The produced outcome.</param>
        /// <returns>Success.</returns>
        private BuildOperationResult OverwriteOccupant(
            SourceArrangement current,
            IBuildBuffer buffer,
            SourceSlot targetSlot,
            SourceSlot replacement,
            out SourceEditOutcome outcome
        )
        {
            buffer.Take(_overwriteItem);
            List<SourceSlot> replacements = new List<SourceSlot> { replacement };
            List<InstanceID> archived = new List<InstanceID> { targetSlot.Instruction.InstanceID };
            outcome = new SourceEditOutcome(current.WithSlots(replacements), archived);
            return BuildOperationResult.Success;
        }

        /// <summary>
        /// Unwraps a Structure per Design §17: archives the Structure, places the Overwrite at the former
        /// header line, and promotes each contained slot to top-level in place.
        /// </summary>
        /// <param name="current">The arrangement to update.</param>
        /// <param name="buffer">The buffer to take the Overwrite from.</param>
        /// <param name="headerSlot">The Structure header target slot.</param>
        /// <param name="overwrite">The Overwrite Instruction instance.</param>
        /// <param name="outcome">The produced outcome.</param>
        /// <returns>Success.</returns>
        private BuildOperationResult OverwriteStructure(
            SourceArrangement current,
            IBuildBuffer buffer,
            SourceSlot headerSlot,
            InstructionInstance overwrite,
            out SourceEditOutcome outcome
        )
        {
            StructureInstance structure = headerSlot.Structure;
            int footprint = structure.Definition.SourceFootprint;
            buffer.Take(_overwriteItem);

            List<SourceSlot> replacements = new List<SourceSlot>();
            replacements.Add(SourceSlot.ForInstruction(_target, overwrite));
            for (int offset = 1; offset < footprint; offset++)
            {
                SourcePosition line = new SourcePosition(_target.LineNumber + offset);
                SourceSlot contained = current.SlotAt(line);
                if (contained.Kind == SourceSlotKind.ContainedInstruction)
                    replacements.Add(SourceSlot.ForInstruction(line, contained.Instruction));
                else
                    replacements.Add(SourceSlot.ForEmpty(line));
            }

            List<InstanceID> archived = new List<InstanceID> { structure.InstanceID };
            outcome = new SourceEditOutcome(current.WithSlots(replacements), archived);
            return BuildOperationResult.Success;
        }
    }
}