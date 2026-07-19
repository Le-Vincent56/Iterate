namespace Iterate.Domain.Content
{
    /// <summary>
    /// The six content categories of the frozen catalog. Serialized in JSON as the uppercase tokens
    /// INSTRUCTION, STRUCTURE, DIRECTIVE, DEPENDENCY, PATCH, UTILITY.
    /// </summary>
    public enum ContentCategory
    {
        Instruction,
        Structure,
        Directive,
        Dependency,
        Patch,
        Utility
    }
}