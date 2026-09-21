using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;
using Iterate.Infrastructure.Content;

namespace Iterate.Editor.Content
{
    /// <summary>
    /// Loads the shipped catalog for Editor tooling, through the identical chain the boot path uses.
    /// Extracted because the same eight lines were already written twice and this would have been the
    /// third; the two existing tools keep their own copies for now, deliberately, since neither has an
    /// automated test behind it.
    ///
    /// The load blocks: Edit mode has no frame loop to await on.
    /// </summary>
    public static class EditorCatalog
    {
        /// <summary>
        /// The catalog folder's name inside StreamingAssets.
        /// </summary>
        public const string CatalogFolder = "Catalog";

        /// <summary>
        /// The shipped catalog's root directory.
        /// </summary>
        /// <returns>The absolute path.</returns>
        public static string ShippedRoot()
        {
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, CatalogFolder);
        }

        /// <summary>
        /// Loads and freezes the shipped catalog.
        /// </summary>
        /// <returns>The frozen catalog.</returns>
        /// <exception cref="CatalogLoadException">Thrown when the catalog fails to load or validate.</exception>
        public static ContentCatalog LoadShipped()
        {
            CatalogLoader loader = new(
                new CatalogJsonReader(),
                new CatalogValidator(),
                new CatalogFreezer(),
                new CatalogDirectorySource(ShippedRoot())
            );

            return Task.Run(() => loader.LoadAsync(CancellationToken.None)).GetAwaiter().GetResult();
        }
    }
}