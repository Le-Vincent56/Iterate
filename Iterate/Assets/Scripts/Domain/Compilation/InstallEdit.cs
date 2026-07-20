using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// Installs a buffered Instruction or Structure into a legal empty destination. An Instruction takes a
    /// top-level or contained empty; a Structure takes a run of contiguous top-level empties, converting
    /// them to a header plus contained empties. The buffer item is taken only after every check passes.
    /// </summary>
    public sealed class InstallEdit : ISourceEdit
    {
        private readonly InstanceID _bufferItem;
        private readonly SourcePosition _destination;

        /// <inheritdoc />
        public bool IsFreeOnly => false;

        /// <summary>
        /// Creates an install edit.
        /// </summary>
        /// <param name="bufferItem">The identity of the buffered item to install.</param>
        /// <param name="destination">The destination position.</param>
        public InstallEdit(InstanceID bufferItem, SourcePosition destination)
        {
            _bufferItem = bufferItem;
            _destination = destination;
        }

        /// <inheritdoc />
        public BuildOperationResult TryApply(SourceArrangement current, IBuildBuffer buffer, out SourceEditOutcome outcome)
        {
            outcome = null;
            if (buffer.TryPeekInstruction(_bufferItem, out InstructionInstance instruction))
                return TryInstallInstruction(current, buffer, instruction, out outcome);

            if (buffer.TryPeekStructure(_bufferItem, out StructureInstance structure))
                return TryInstallStructure(current, buffer, structure, out outcome);

            return BuildOperationResult.Rejected(SourceEditRejection.ItemNotInBuffer);
        }

        /// <summary>
        /// Installs an Instruction into a top-level or contained empty destination.
        /// </summary>
        /// <param name="current">The arrangement to validate against.</param>
        /// <param name="buffer">The buffer to take from on success.</param>
        /// <param name="instruction">The Instruction being installed.</param>
        /// <param name="outcome">The produced outcome on success; null when rejected.</param>
        /// <returns>Success, or a typed rejection.</returns>
        private BuildOperationResult TryInstallInstruction(
            SourceArrangement current,
            IBuildBuffer buffer,
            InstructionInstance instruction,
            out SourceEditOutcome outcome)
        {
            outcome = null;
            if (_destination.LineNumber > current.SlotCount)
                return BuildOperationResult.Rejected(SourceEditRejection.PositionNotFound);

            SourceSlot slot = current.SlotAt(_destination);
            if (slot.Kind == SourceSlotKind.Core)
                return BuildOperationResult.Rejected(SourceEditRejection.CoreImmutable);

            if (slot.Kind == SourceSlotKind.Empty)
            {
                buffer.Take(_bufferItem);
                outcome = SingleSlotOutcome(current, SourceSlot.ForInstruction(_destination, instruction));
                return BuildOperationResult.Success;
            }

            if (slot.Kind != SourceSlotKind.ContainedEmpty)
                return BuildOperationResult.Rejected(SourceEditRejection.DestinationNotEmpty);
            
            buffer.Take(_bufferItem);
            outcome = SingleSlotOutcome(current, SourceSlot.ForContainedInstruction(_destination, slot.Structure, instruction));
            return BuildOperationResult.Success;
        }

        /// <summary>
        /// Installs a Structure across a run of contiguous top-level empties.
        /// </summary>
        /// <param name="current">The arrangement to validate against.</param>
        /// <param name="buffer">The buffer to take from on success.</param>
        /// <param name="structure">The Structure being installed.</param>
        /// <param name="outcome">The produced outcome on success; null when rejected.</param>
        /// <returns>Success, or a typed rejection.</returns>
        private BuildOperationResult TryInstallStructure(
            SourceArrangement current,
            IBuildBuffer buffer,
            StructureInstance structure,
            out SourceEditOutcome outcome
        )
        {
            outcome = null;
            if (_destination.LineNumber > current.SlotCount)
                return BuildOperationResult.Rejected(SourceEditRejection.PositionNotFound);

            SourceSlot destinationSlot = current.SlotAt(_destination);
            if (destinationSlot.Kind == SourceSlotKind.Core)
                return BuildOperationResult.Rejected(SourceEditRejection.CoreImmutable);

            if (destinationSlot.Kind == SourceSlotKind.ContainedEmpty || destinationSlot.Kind == SourceSlotKind.ContainedInstruction)
                return BuildOperationResult.Rejected(SourceEditRejection.NestingNotSupported);

            if (destinationSlot.Kind != SourceSlotKind.Empty)
                return BuildOperationResult.Rejected(SourceEditRejection.DestinationNotEmpty);

            int footprint = structure.Definition.SourceFootprint;
            for (int offset = 1; offset < footprint; offset++)
            {
                int line = _destination.LineNumber + offset;
                if (line > current.SlotCount)
                    return BuildOperationResult.Rejected(SourceEditRejection.InsufficientFootprint);

                if (current.SlotAt(new SourcePosition(line)).Kind != SourceSlotKind.Empty)
                    return BuildOperationResult.Rejected(SourceEditRejection.InsufficientFootprint);
            }

            buffer.Take(_bufferItem);
            List<SourceSlot> replacements = new List<SourceSlot>();
            replacements.Add(SourceSlot.ForStructureHeader(_destination, structure));
            for (int offset = 1; offset < footprint; offset++)
            {
                replacements.Add(SourceSlot.ForContainedEmpty(new SourcePosition(_destination.LineNumber + offset), structure));
            }

            outcome = new SourceEditOutcome(current.WithSlots(replacements), Array.Empty<InstanceID>());
            return BuildOperationResult.Success;
        }

        /// <summary>
        /// Builds an outcome replacing a single slot and archiving nothing.
        /// </summary>
        /// <param name="current">The arrangement to update.</param>
        /// <param name="slot">The replacement slot.</param>
        /// <returns>An outcome carrying the updated arrangement and an empty archive.</returns>
        private SourceEditOutcome SingleSlotOutcome(SourceArrangement current, SourceSlot slot)
        {
            List<SourceSlot> replacements = new List<SourceSlot> { slot };
            return new SourceEditOutcome(current.WithSlots(replacements), Array.Empty<InstanceID>());
        }
    }
}