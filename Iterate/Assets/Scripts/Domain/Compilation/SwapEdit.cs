using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// Swaps two occupied targets: Instruction with Instruction across top-level and contained slots, or
    /// Structure with Structure of equal total footprint carrying contained occupants. Structure-to-single,
    /// unequal footprints, empties, and Core are rejected. The buffer is never touched.
    /// </summary>
    public sealed class SwapEdit : ISourceEdit
    {
        private readonly SourcePosition _first;
        private readonly SourcePosition _second;

        /// <inheritdoc />
        public bool IsFreeOnly => false;

        /// <summary>
        /// Creates a swap edit.
        /// </summary>
        /// <param name="first">The first target position.</param>
        /// <param name="second">The second target position.</param>
        public SwapEdit(SourcePosition first, SourcePosition second)
        {
            _first = first;
            _second = second;
        }

        /// <inheritdoc />
        public BuildOperationResult TryApply(SourceArrangement current, IBuildBuffer buffer,
            out SourceEditOutcome outcome)
        {
            outcome = null;
            if (_first.LineNumber > current.SlotCount || _second.LineNumber > current.SlotCount)
                return BuildOperationResult.Rejected(SourceEditRejection.PositionNotFound);

            SourceSlot a = current.SlotAt(_first);
            SourceSlot b = current.SlotAt(_second);
            if (a.Kind == SourceSlotKind.Core || b.Kind == SourceSlotKind.Core)
                return BuildOperationResult.Rejected(SourceEditRejection.CoreImmutable);

            if (IsInstruction(a) && IsInstruction(b))
                return SwapInstructions(current, a, b, out outcome);

            if (a.Kind == SourceSlotKind.StructureHeader && b.Kind == SourceSlotKind.StructureHeader)
                return SwapStructures(current, a, b, out outcome);

            return BuildOperationResult.Rejected(SourceEditRejection.IncompatibleSwap);
        }

        /// <summary>
        /// Whether a slot holds an Instruction, top-level or contained.
        /// </summary>
        /// <param name="slot">The slot to test.</param>
        /// <returns>True when the slot holds an Instruction.</returns>
        private bool IsInstruction(SourceSlot slot)
        {
            return slot.Kind == SourceSlotKind.Instruction || slot.Kind == SourceSlotKind.ContainedInstruction;
        }

        /// <summary>
        /// Exchanges two Instruction occupants, each keeping its slot's containment context.
        /// </summary>
        /// <param name="current">The arrangement to update.</param>
        /// <param name="a">The first Instruction slot.</param>
        /// <param name="b">The second Instruction slot.</param>
        /// <param name="outcome">The produced outcome.</param>
        /// <returns>Success.</returns>
        private BuildOperationResult SwapInstructions(
            SourceArrangement current,
            SourceSlot a,
            SourceSlot b,
            out SourceEditOutcome outcome)
        {
            SourceSlot newFirst = a.Kind == SourceSlotKind.ContainedInstruction
                ? SourceSlot.ForContainedInstruction(_first, a.Structure, b.Instruction)
                : SourceSlot.ForInstruction(_first, b.Instruction);

            SourceSlot newSecond = b.Kind == SourceSlotKind.ContainedInstruction
                ? SourceSlot.ForContainedInstruction(_second, b.Structure, a.Instruction)
                : SourceSlot.ForInstruction(_second, a.Instruction);

            List<SourceSlot> replacements = new List<SourceSlot> { newFirst, newSecond };
            outcome = new SourceEditOutcome(current.WithSlots(replacements), Array.Empty<InstanceID>());
            return BuildOperationResult.Success;
        }

        /// <summary>
        /// Exchanges two equal-footprint Structure spans, carrying contained occupants along.
        /// </summary>
        /// <param name="current">The arrangement to update.</param>
        /// <param name="firstHeader">The first Structure header slot.</param>
        /// <param name="secondHeader">The second Structure header slot.</param>
        /// <param name="outcome">The produced outcome on success; null when rejected.</param>
        /// <returns>Success, or a typed rejection.</returns>
        private BuildOperationResult SwapStructures(
            SourceArrangement current,
            SourceSlot firstHeader,
            SourceSlot secondHeader,
            out SourceEditOutcome outcome
        )
        {
            outcome = null;
            StructureInstance firstStructure = firstHeader.Structure;
            StructureInstance secondStructure = secondHeader.Structure;
            int footprint = firstStructure.Definition.SourceFootprint;
            if (footprint != secondStructure.Definition.SourceFootprint)
                return BuildOperationResult.Rejected(SourceEditRejection.IncompatibleSwap);

            List<SourceSlot> replacements = new List<SourceSlot>();
            for (int offset = 0; offset < footprint; offset++)
            {
                SourceSlot secondSpanSlot = current.SlotAt(new SourcePosition(_second.LineNumber + offset));
                replacements.Add(RelocatedStructureSlot(new SourcePosition(_first.LineNumber + offset), secondStructure,
                    secondSpanSlot, offset));

                SourceSlot firstSpanSlot = current.SlotAt(new SourcePosition(_first.LineNumber + offset));
                replacements.Add(RelocatedStructureSlot(new SourcePosition(_second.LineNumber + offset), firstStructure, firstSpanSlot, offset));
            }

            outcome = new SourceEditOutcome(current.WithSlots(replacements), Array.Empty<InstanceID>());
            return BuildOperationResult.Success;
        }

        /// <summary>
        /// Rebuilds a Structure span slot at a new position under a new owning Structure.
        /// </summary>
        /// <param name="position">The destination position.</param>
        /// <param name="structure">The owning Structure at the destination.</param>
        /// <param name="source">The source span slot supplying the occupant.</param>
        /// <param name="offset">The offset within the span; zero is the header.</param>
        /// <returns>The rebuilt slot.</returns>
        private SourceSlot RelocatedStructureSlot(
            SourcePosition position,
            StructureInstance structure,
            SourceSlot source,
            int offset
        )
        {
            if (offset == 0)
                return SourceSlot.ForStructureHeader(position, structure);

            if (source.Kind == SourceSlotKind.ContainedInstruction)
                return SourceSlot.ForContainedInstruction(position, structure, source.Instruction);

            return SourceSlot.ForContainedEmpty(position, structure);
        }
    }
}