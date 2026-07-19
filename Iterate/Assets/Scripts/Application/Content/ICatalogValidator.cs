using System.Collections.Generic;

namespace Iterate.Application.Content
{
    /// <summary>
    /// The catalog validation seam the loader depends on: returns the complete error list for a parsed
    /// file set, empty when valid. Implemented by <see cref="CatalogValidator"/>; abstracted so the
    /// loader's freezer-escape path is testable and so Composition can register the validator by role.
    /// </summary>
    public interface ICatalogValidator
    {
        /// <summary>
        /// Validates a parsed catalog file set, returning the complete error list.
        /// </summary>
        /// <param name="files">The parsed manifest and content files.</param>
        /// <returns>The complete error list, empty when valid.</returns>
        IReadOnlyList<CatalogError> Validate(CatalogFileSet files);
    }
}