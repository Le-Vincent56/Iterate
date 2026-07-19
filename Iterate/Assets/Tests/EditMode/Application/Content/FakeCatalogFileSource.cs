using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Content;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// An in-memory <see cref="ICatalogFileSource"/> for loader tests: serves a manifest and named
    /// files from dictionaries, and can be told to make the manifest or specific files unreadable to
    /// exercise the loader's I/O-failure paths.
    /// </summary>
    public sealed class FakeCatalogFileSource : ICatalogFileSource
    {
        private readonly string _manifest;

        private readonly bool _manifestUnreadable;

        private readonly IReadOnlyDictionary<string, string> _files;

        private readonly HashSet<string> _unreadable;

        public FakeCatalogFileSource(string manifest, IReadOnlyDictionary<string, string> files)
            : this(manifest, files, false, null)
        {
        }

        public FakeCatalogFileSource(
            string manifest,
            IReadOnlyDictionary<string, string> files,
            bool manifestUnreadable,
            IEnumerable<string> unreadableFiles
        )
        {
            _manifest = manifest;
            _files = files;
            _manifestUnreadable = manifestUnreadable;
            _unreadable = new HashSet<string>(StringComparer.Ordinal);
            if (unreadableFiles != null)
            {
                foreach (string fileName in unreadableFiles)
                {
                    _unreadable.Add(fileName);
                }
            }
        }

        /// <summary>
        /// Returns the manifest text, or throws when configured unreadable.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The manifest text.</returns>
        public Task<string> ReadManifestAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_manifestUnreadable)
            {
                throw new IOException("the manifest is unreadable.");
            }

            return Task.FromResult(_manifest);
        }

        /// <summary>
        /// Returns a named file's text, throwing when the file is configured unreadable or absent.
        /// </summary>
        /// <param name="fileName">The file name to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The file text.</returns>
        public Task<string> ReadFileAsync(string fileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_unreadable.Contains(fileName))
            {
                throw new IOException("the file '" + fileName + "' is unreadable.");
            }

            if (_files.TryGetValue(fileName, out string text))
            {
                return Task.FromResult(text);
            }

            throw new FileNotFoundException("the file '" + fileName + "' is missing.");
        }
    }
}
