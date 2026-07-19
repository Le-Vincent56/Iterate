namespace Iterate.Domain.Content
{
    /// <summary>
    /// The kind of catalog file a manifest entry declares. Maps one-to-one onto the manifest JSON
    /// category tokens (PARAMETERS, INSTRUCTION, STRUCTURE, DIRECTIVE, DEPENDENCY, PATCH, UTILITY); the
    /// freezer maps the content kinds onto <see cref="ContentCategory"/>.
    /// </summary>
    public enum CatalogFileKind
    {
        Parameters,
        Instruction,
        Structure,
        Directive,
        Dependency,
        Patch,
        Utility
    }
}