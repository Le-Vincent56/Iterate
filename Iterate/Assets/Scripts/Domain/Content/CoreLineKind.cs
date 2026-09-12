namespace Iterate.Domain.Content
{
    /// <summary>
    /// The closed set of line kinds an authored Core carries: a fixed instruction the Core owns, an
    /// open position the player fills, or a fixed Structure the Core owns together with the single
    /// fixed instruction it contains. Serialized in JSON as FIXED_INSTRUCTION, OPEN, FIXED_STRUCTURE.
    /// </summary>
    public enum CoreLineKind
    {
        FixedInstruction,
        Open,
        FixedStructure
    }
}