using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Persistence;

namespace Iterate.Application.Persistence.Tests
{
    /// <summary>
    /// An in-memory <see cref="ISaveFileStore"/> for service tests: holds file text in a dictionary, can
    /// be told which files are unreadable or unwritable to exercise the failure paths, and records the
    /// file names written and deleted in order so a test can assert what happened and in which sequence.
    /// Only cancellation throws, exactly as the real adapter behaves.
    /// </summary>
    public sealed class FakeSaveFileStore : ISaveFileStore
    {
        private readonly List<string> _writes = new();
        private readonly List<string> _deletes = new();

        /// <summary>
        /// The file text held, by file name. A test may seed or inspect it directly.
        /// </summary>
        public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// File names whose reads answer <see cref="SaveFileReadOutcome.Unreadable"/>.
        /// </summary>
        public HashSet<string> Unreadable { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// File names whose writes and deletes fail.
        /// </summary>
        public HashSet<string> Unwritable { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// The file names written, in order.
        /// </summary>
        public IReadOnlyList<string> Writes => _writes;

        /// <summary>
        /// The file names deleted, in order.
        /// </summary>
        public IReadOnlyList<string> Deletes => _deletes;

        /// <summary>
        /// Reads a file's text, or reports it absent or unreadable.
        /// </summary>
        /// <param name="fileName">The file to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The read result.</returns>
        public Task<SaveFileReadResult> ReadAsync(string fileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Unreadable.Contains(fileName))
                return Task.FromResult(SaveFileReadResult.Unreadable("fake"));

            if (Files.TryGetValue(fileName, out string text))
                return Task.FromResult(SaveFileReadResult.Found(text));

            return Task.FromResult(SaveFileReadResult.Absent());
        }

        /// <summary>
        /// Writes a file's text, or reports the write failed.
        /// </summary>
        /// <param name="fileName">The file to write.</param>
        /// <param name="text">The text to write.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The write result.</returns>
        public Task<SaveFileWriteResult> WriteAsync(string fileName, string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _writes.Add(fileName);
            if (Unwritable.Contains(fileName))
                return Task.FromResult(SaveFileWriteResult.Failed("fake"));

            Files[fileName] = text;
            return Task.FromResult(SaveFileWriteResult.Written());
        }

        /// <summary>
        /// Deletes a file, or reports it absent or that the delete failed.
        /// </summary>
        /// <param name="fileName">The file to delete.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The delete result.</returns>
        public Task<SaveFileDeleteResult> DeleteAsync(string fileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _deletes.Add(fileName);
            if (Unwritable.Contains(fileName))
                return Task.FromResult(SaveFileDeleteResult.Failed("fake"));

            if (!Files.Remove(fileName))
                return Task.FromResult(SaveFileDeleteResult.Absent());

            return Task.FromResult(SaveFileDeleteResult.Deleted());
        }
    }
}
