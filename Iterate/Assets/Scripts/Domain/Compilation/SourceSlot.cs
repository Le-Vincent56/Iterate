using Iterate.Domain.Content;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// One position in a source arrangement paired with exactly one legal occupant shape. The private
    /// constructor and per-kind static factories make an invalid combination — e.g. an Instruction slot
    /// that also carries a Core line, or a contained slot holding a Structure — unrepresentable.
    /// </summary>
    public sealed record SourceSlot
    {
        /// <summary>
        /// The numbered position this slot occupies.
        /// </summary>
        public SourcePosition Position { get; }

        /// <summary>
        /// The slot's kind, selecting which occupant field is populated.
        /// </summary>
        public SourceSlotKind Kind { get; }

        /// <summary>
        /// The Core-owned line for a <see cref="SourceSlotKind.Core"/> slot; null otherwise.
        /// </summary>
        public CoreLine Core { get; }

        /// <summary>
        /// The occupying Instruction for an Instruction or contained-Instruction slot; null otherwise.
        /// </summary>
        public InstructionInstance Instruction { get; }

        /// <summary>
        /// The owning Structure for a header, contained-empty, or contained-Instruction slot; null
        /// otherwise.
        /// </summary>
        public StructureInstance Structure { get; }

        private SourceSlot(
            SourcePosition position,
            SourceSlotKind kind,
            CoreLine core,
            InstructionInstance instruction,
            StructureInstance structure
        )
        {
            Position = position;
            Kind = kind;
            Core = core;
            Instruction = instruction;
            Structure = structure;
        }

        /// <summary>
        /// Creates an immutable Core-owned slot.
        /// </summary>
        /// <param name="position">The slot position.</param>
        /// <param name="core">The Core-owned line.</param>
        /// <returns>A Core slot carrying the line and no player occupant.</returns>
        public static SourceSlot ForCore(SourcePosition position, CoreLine core)
        {
            return new SourceSlot(position, SourceSlotKind.Core, core, null, null);
        }

        /// <summary>
        /// Creates an empty top-level player slot.
        /// </summary>
        /// <param name="position">The slot position.</param>
        /// <returns>An empty slot with no occupant.</returns>
        public static SourceSlot ForEmpty(SourcePosition position)
        {
            return new SourceSlot(position, SourceSlotKind.Empty, null, null, null);
        }

        /// <summary>
        /// Creates a top-level slot holding a player Instruction.
        /// </summary>
        /// <param name="position">The slot position.</param>
        /// <param name="instruction">The occupying Instruction instance.</param>
        /// <returns>An Instruction slot carrying the occupant.</returns>
        public static SourceSlot ForInstruction(SourcePosition position, InstructionInstance instruction)
        {
            return new SourceSlot(position, SourceSlotKind.Instruction, null, instruction, null);
        }

        /// <summary>
        /// Creates a Structure header slot.
        /// </summary>
        /// <param name="position">The header position.</param>
        /// <param name="structure">The Structure instance the header introduces.</param>
        /// <returns>A header slot owned by the Structure.</returns>
        public static SourceSlot ForStructureHeader(SourcePosition position, StructureInstance structure)
        {
            return new SourceSlot(position, SourceSlotKind.StructureHeader, null, null, structure);
        }

        /// <summary>
        /// Creates an empty slot contained within a Structure.
        /// </summary>
        /// <param name="position">The contained position.</param>
        /// <param name="structure">The owning Structure instance.</param>
        /// <returns>A contained-empty slot owned by the Structure.</returns>
        public static SourceSlot ForContainedEmpty(SourcePosition position, StructureInstance structure)
        {
            return new SourceSlot(position, SourceSlotKind.ContainedEmpty, null, null, structure);
        }

        /// <summary>
        /// Creates a contained slot holding a player Instruction.
        /// </summary>
        /// <param name="position">The contained position.</param>
        /// <param name="structure">The owning Structure instance.</param>
        /// <param name="instruction">The occupying Instruction instance.</param>
        /// <returns>A contained-Instruction slot owned by the Structure and carrying the occupant.</returns>
        public static SourceSlot ForContainedInstruction(
            SourcePosition position,
            StructureInstance structure,
            InstructionInstance instruction)
        {
            return new SourceSlot(position, SourceSlotKind.ContainedInstruction, null, instruction, structure);
        }
    }
}