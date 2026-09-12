namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The three content kinds a Repository holds. Dependencies, Patches, Utilities and Process rules
    /// are never Repository items: a Dependency is installed, a Patch is socketed onto a host, a
    /// Utility resolves at Process setup, and a Process rule belongs to its Process.
    /// </summary>
    public enum RepositoryItemKind
    {
        Instruction,
        Structure,
        Directive
    }
}