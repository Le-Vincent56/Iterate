using System.Collections.Generic;
using Iterate.Application.Content.Json;

namespace Iterate.Application.Content
{
    /// <summary>
    /// The parsed catalog inputs: the manifest value tree and each content file's value tree keyed by
    /// file name. The loader parses raw text into this shape before validation.
    /// </summary>
    /// <param name="Manifest">The parsed manifest value tree.</param>
    /// <param name="ContentFiles">The parsed content files keyed by file name.</param>
    public sealed record CatalogFileSet(JsonValue Manifest, IReadOnlyDictionary<string, JsonValue> ContentFiles);
}