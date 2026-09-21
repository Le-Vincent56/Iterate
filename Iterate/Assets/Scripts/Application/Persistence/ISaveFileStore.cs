using System.Threading;
using System.Threading.Tasks;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The Application-side port for the three save files: text in, text out, by file name. Every
    /// failure a caller can do something about is an outcome rather than an exception — a save file is
    /// data the game found on disk, so an unreadable or unwritable one is an expected state of the world
    /// rather than a broken contract. Only cancellation throws.
    ///
    /// Implementations are engine-free; the platform path is bound in Composition.
    /// </summary>
    public interface ISaveFileStore
    {
        /// <summary>
        /// Reads a save file's text.
        /// </summary>
        /// <param name="fileName">The file to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The read result.</returns>
        Task<SaveFileReadResult> ReadAsync(string fileName, CancellationToken cancellationToken);

        /// <summary>
        /// Writes a save file's text, replacing any existing file.
        /// </summary>
        /// <param name="fileName">The file to write.</param>
        /// <param name="text">The text to write.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The write result.</returns>
        Task<SaveFileWriteResult> WriteAsync(string fileName, string text, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes a save file.
        /// </summary>
        /// <param name="fileName">The file to delete.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The delete result.</returns>
        Task<SaveFileDeleteResult> DeleteAsync(string fileName, CancellationToken cancellationToken);
    }
}