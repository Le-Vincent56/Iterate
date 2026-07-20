namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The kind of a single source slot, selecting which occupant a slot legally carries: an immutable
    /// Core line, an empty top-level player position, a top-level Instruction, a Structure header, or a
    /// slot contained within a Structure (empty or holding an Instruction).
    /// </summary>
    public enum SourceSlotKind
    {
        /// <summary>
        /// An immutable Core-owned line.
        /// </summary>
        Core,

        /// <summary>
        /// An empty, legal top-level player position.
        /// </summary>
        Empty,

        /// <summary>
        /// A top-level position holding a player Instruction.
        /// </summary>
        Instruction,

        /// <summary>
        /// The header position of an installed Structure.
        /// </summary>
        StructureHeader,

        /// <summary>
        /// An empty position contained within a Structure.
        /// </summary>
        ContainedEmpty,

        /// <summary>
        /// A contained position holding a player Instruction.
        /// </summary>
        ContainedInstruction
    }
}