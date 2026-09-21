using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Persistence;

namespace Iterate.Infrastructure.Persistence
{
    /// <summary>
    /// The file-system adapter behind the save port: one file per name inside a root directory. Every
    /// failure a caller can do something about becomes an outcome; only cancellation throws.
    ///
    /// Writes go to a temporary file first and are renamed into place, so a write interrupted part way
    /// through cannot leave a half-written save where a whole one used to be. A failure in either window
    /// removes the temporary file, because a stray one is invisible to the read path and would otherwise
    /// accumulate unnoticed.
    ///
    /// The root directory is supplied by Composition; nothing here knows about the engine.
    /// </summary>
    public sealed class FileSaveStore : ISaveFileStore
    {
        private const string TemporarySuffix = ".tmp";

        private readonly string _rootDirectory;

        public FileSaveStore(string rootDirectory)
        {
            if (string.IsNullOrEmpty(rootDirectory))
                throw new ArgumentException("A save file store requires a root directory.", nameof(rootDirectory));

            _rootDirectory = rootDirectory;
        }

        /// <summary>
        /// Reads a save file's text.
        /// </summary>
        /// <param name="fileName">The file to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The read result.</returns>
        public async Task<SaveFileReadResult> ReadAsync(string fileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(_rootDirectory, fileName);
            try
            {
                if (Directory.Exists(path))
                    return SaveFileReadResult.Unreadable("A directory occupies the save file's path.");
                
                if (!File.Exists(path))
                    return SaveFileReadResult.Absent();

                string text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                return SaveFileReadResult.Found(text);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException exception)
            {
                return SaveFileReadResult.Unreadable(exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return SaveFileReadResult.Unreadable(exception.Message);
            }
        }

        /// <summary>
        /// Writes a save file's text, replacing any existing file only once the new text is safely on
        /// disk.
        /// </summary>
        /// <param name="fileName">The file to write.</param>
        /// <param name="text">The text to write.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The write result.</returns>
        public async Task<SaveFileWriteResult> WriteAsync(
            string fileName,
            string text,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(_rootDirectory, fileName);
            string temporary = path + TemporarySuffix;
            try
            {
                Directory.CreateDirectory(_rootDirectory);
                await File
                    .WriteAllTextAsync(temporary, text, new UTF8Encoding(false), cancellationToken)
                    .ConfigureAwait(false);

                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);

                return SaveFileWriteResult.Written();
            }
            catch (OperationCanceledException)
            {
                DiscardTemporary(temporary);
                throw;
            }
            catch (IOException exception)
            {
                DiscardTemporary(temporary);
                return SaveFileWriteResult.Failed(exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                DiscardTemporary(temporary);
                return SaveFileWriteResult.Failed(exception.Message);
            }
        }

        /// <summary>
        /// Deletes a save file.
        /// </summary>
        /// <param name="fileName">The file to delete.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The delete result.</returns>
        public Task<SaveFileDeleteResult> DeleteAsync(string fileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(_rootDirectory, fileName);
            try
            {
                if (Directory.Exists(path))
                    return Task.FromResult(SaveFileDeleteResult.Failed("A directory occupies the save file's path."));
                
                if (!File.Exists(path))
                    return Task.FromResult(SaveFileDeleteResult.Absent());

                File.Delete(path);
                return Task.FromResult(SaveFileDeleteResult.Deleted());
            }
            catch (IOException exception)
            {
                return Task.FromResult(SaveFileDeleteResult.Failed(exception.Message));
            }
            catch (UnauthorizedAccessException exception)
            {
                return Task.FromResult(SaveFileDeleteResult.Failed(exception.Message));
            }
        }

        /// <summary>
        /// Removes a temporary write file, ignoring any failure. The caller is already reporting the
        /// failure that brought it here, and a second one would replace a useful message with a
        /// misleading one.
        /// </summary>
        /// <param name="temporary">The temporary file's path.</param>
        private static void DiscardTemporary(string temporary)
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}