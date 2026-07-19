using System.Threading;
using System.Threading.Tasks;

namespace Iterate.Application.Content
{
    /// <summary>
    /// The Application-side port for reading raw catalog text: a manifest and named
    /// content files. Implementations are engine-free; the engine path binding happens in Composition
    /// and the Editor tool.
    /// </summary>
    public interface ICatalogFileSource
    {
        /// <summary>
        /// Reads the manifest file's raw text.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The manifest text.</returns>
        Task<string> ReadManifestAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Reads a named content file's raw text.
        /// </summary>
        /// <param name="fileName">The content file name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The file text.</returns>
        Task<string> ReadFileAsync(string fileName, CancellationToken cancellationToken);
    }
}