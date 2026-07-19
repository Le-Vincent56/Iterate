using System.Collections.Generic;

namespace Iterate.Application.Content
{
    /// <summary>
    /// The typed projection of a validated manifest: the catalog revision, the schema version, and the
    /// ordered file entries. Built by the loader after validation for the freezer's consumption; the
    /// validator interprets the raw manifest value tree directly.
    /// </summary>
    /// <param name="Revision">The catalog revision identity.</param>
    /// <param name="SchemaVersion">The schema version the catalog targets.</param>
    /// <param name="Files">The manifest file entries in document order.</param>
    public sealed record CatalogManifest(
        string Revision,
        string SchemaVersion,
        IReadOnlyList<CatalogManifestEntry> Files
    );
}