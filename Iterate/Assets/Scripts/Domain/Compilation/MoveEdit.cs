using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// Moves an occupied object to an empty compatible destination: a single Instruction across top-level
    /// and contained slots, or a whole Structure carrying its contained occupants by relative offset. The
    /// origin is left empty. The buffer is never touched.
    /// </summary>
    public sealed class MoveEdit : ISourceEdit
    {
        private readonly SourcePosition _origin;
        private readonly SourcePosition _destination;

        /// <inheritdoc />
        public bool IsFreeOnly => false;

        /// <summary>
        /// Creates a move edit.
        /// </summary>
        /// <param name="origin">The occupied origin position.</param>
        /// <param name="destination">The empty destination position.</param>
        public MoveEdit(SourcePosition origin, SourcePosition destination)
        {
            _origin = origin;
            _destination = destination;
        }

        /// <inheritdoc />
        public BuildOperationResult TryApply(SourceArrangement current, IBuildBuffer buffer, out SourceEditOutcome outcome)
        {
            outcome = null;
            if (_origin.LineNumber > current.SlotCount || _destination.LineNumber > current.SlotCount)
                return BuildOperationResult.Rejected(SourceEditRejection.PositionNotFound);

            SourceSlot originSlot = current.SlotAt(_origin);
            switch (originSlot.Kind)
            {
                case SourceSlotKind.Core:
                    return BuildOperationResult.Rejected(SourceEditRejection.CoreImmutable);
                case SourceSlotKind.Empty:
                case SourceSlotKind.ContainedEmpty:
                    return BuildOperationResult.Rejected(SourceEditRejection.OriginEmpty);
                case SourceSlotKind.Instruction:
                case SourceSlotKind.ContainedInstruction:
                    return TryMoveInstruction(current, originSlot, out outcome);
                case SourceSlotKind.StructureHeader:
                    return TryMoveStructure(current, originSlot, out outcome);
                default:
                    return BuildOperationResult.Rejected(SourceEditRejection.OriginEmpty);
            }
        }

        /// <summary>
        /// Moves a single Instruction to a compatible empty destination, vacating the origin.
        /// </summary>
        /// <param name="current">The arrangement to validate against.</param>
        /// <param name="originSlot">The Instruction origin slot.</param>
        /// <param name="outcome">The produced outcome on success; null when rejected.</param>
        /// <returns>Success, or a typed rejection.</returns>
        private BuildOperationResult TryMoveInstruction(SourceArrangement current, SourceSlot originSlot, out SourceEditOutcome outcome)
        {
            outcome = null;
            SourceSlot destinationSlot = current.SlotAt(_destination);
            if (destinationSlot.Kind == SourceSlotKind.Core)
                return BuildOperationResult.Rejected(SourceEditRejection.CoreImmutable);

            InstructionInstance moving = originSlot.Instruction;
            SourceSlot placed;
            if (destinationSlot.Kind == SourceSlotKind.Empty)
                placed = SourceSlot.ForInstruction(_destination, moving);
            else if (destinationSlot.Kind == SourceSlotKind.ContainedEmpty)
                placed = SourceSlot.ForContainedInstruction(_destination, destinationSlot.Structure, moving);
            else
                return BuildOperationResult.Rejected(SourceEditRejection.DestinationNotEmpty);

            SourceSlot vacated = originSlot.Kind == SourceSlotKind.ContainedInstruction
                ? SourceSlot.ForContainedEmpty(_origin, originSlot.Structure)
                : SourceSlot.ForEmpty(_origin);

            List<SourceSlot> replacements = new List<SourceSlot> { vacated, placed };
            outcome = new SourceEditOutcome(current.WithSlots(replacements), Array.Empty<InstanceID>());
            return BuildOperationResult.Success;
        }

        /// <summary>
        /// Moves a whole Structure to a run of contiguous empties, carrying contained occupants along.
        /// </summary>
        /// <param name="current">The arrangement to validate against.</param>
        /// <param name="headerSlot">The Structure header origin slot.</param>
        /// <param name="outcome">The produced outcome on success; null when rejected.</param>
        /// <returns>Success, or a typed rejection.</returns>
        private BuildOperationResult TryMoveStructure(SourceArrangement current, SourceSlot headerSlot, out SourceEditOutcome outcome)
        {
            outcome = null;
            StructureInstance structure = headerSlot.Structure;
            int footprint = structure.Definition.SourceFootprint;
            int originStart = _origin.LineNumber;
            int destinationStart = _destination.LineNumber;

            SourceSlot destinationSlot = current.SlotAt(_destination);
            if (destinationSlot.Kind == SourceSlotKind.Core)
                return BuildOperationResult.Rejected(SourceEditRejection.CoreImmutable);

            if (destinationSlot.Kind == SourceSlotKind.ContainedEmpty || destinationSlot.Kind == SourceSlotKind.ContainedInstruction)
                return BuildOperationResult.Rejected(SourceEditRejection.NestingNotSupported);

            for (int offset = 0; offset < footprint; offset++)
            {
                int line = destinationStart + offset;
                if (line > current.SlotCount)
                    return BuildOperationResult.Rejected(SourceEditRejection.InsufficientFootprint);

                bool withinOrigin = line >= originStart && line <= originStart + footprint - 1;
                SourceSlot spanSlot = current.SlotAt(new SourcePosition(line));
                if (spanSlot.Kind == SourceSlotKind.Core)
                    return BuildOperationResult.Rejected(SourceEditRejection.CoreImmutable);

                if (!withinOrigin && spanSlot.Kind != SourceSlotKind.Empty)
                    return BuildOperationResult.Rejected(SourceEditRejection.InsufficientFootprint);
            }

            List<SourceSlot> replacements = new List<SourceSlot>();
            for (int offset = 0; offset < footprint; offset++)
            {
                replacements.Add(SourceSlot.ForEmpty(new SourcePosition(originStart + offset)));
            }

            for (int offset = 0; offset < footprint; offset++)
            {
                SourceSlot originSpanSlot = current.SlotAt(new SourcePosition(originStart + offset));
                SourcePosition destinationLine = new SourcePosition(destinationStart + offset);
                if (offset == 0)
                    replacements.Add(SourceSlot.ForStructureHeader(destinationLine, structure));
                else if (originSpanSlot.Kind == SourceSlotKind.ContainedInstruction)
                    replacements.Add(SourceSlot.ForContainedInstruction(destinationLine, structure, originSpanSlot.Instruction));
                else
                    replacements.Add(SourceSlot.ForContainedEmpty(destinationLine, structure));
            }

            outcome = new SourceEditOutcome(current.WithSlots(replacements), Array.Empty<InstanceID>());
            return BuildOperationResult.Success;
        }
    }
}