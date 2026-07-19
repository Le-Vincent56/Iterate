using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Content;

namespace Iterate.Infrastructure.Content
{
    /// <summary>
    /// An <see cref="ICatalogFileSource"/> that reads the manifest and named files from a directory on
    /// disk. Engine-free by construction (System.IO only); the engine-path binding happens in
    /// Composition and the Editor tool. Read failures are wrapped into an <see cref="IOException"/>
    /// naming the file; the loader converts these into <c>file.unreadable</c> errors.
    /// </summary>
    public sealed class CatalogDirectorySource : ICatalogFileSource
    {
        private const string ManifestFileName = "manifest.json";

        private readonly string _rootDirectory;

        public CatalogDirectorySource(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
        }

        /// <summary>
        /// Reads the manifest file's text from the root directory.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The manifest text.</returns>
        public async Task<string> ReadManifestAsync(CancellationToken cancellationToken)
        {
            return await ReadAsync(ManifestFileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads a named content file's text from the root directory.
        /// </summary>
        /// <param name="fileName">The content file name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The file text.</returns>
        public async Task<string> ReadFileAsync(string fileName, CancellationToken cancellationToken)
        {
            return await ReadAsync(fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads a file's text from the root directory, wrapping I/O failures with the file name.
        /// </summary>
        /// <param name="fileName">The file name to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The file text.</returns>
        private async Task<string> ReadAsync(string fileName, CancellationToken cancellationToken)
        {
            string path = Path.Combine(_rootDirectory, fileName);
            try
            {
                return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException exception)
            {
                throw new IOException("could not read the catalog file '" + fileName + "'.", exception);
            }
        }
    }
}