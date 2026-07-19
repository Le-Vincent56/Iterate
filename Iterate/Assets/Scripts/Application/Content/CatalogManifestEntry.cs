using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// One manifest entry: a content file name and the kind of definitions it carries.
    /// </summary>
    /// <param name="File">The content file name.</param>
    /// <param name="Kind">The kind of file.</param>
    public sealed record CatalogManifestEntry(string File, CatalogFileKind Kind);
}