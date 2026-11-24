using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Reads, exports and deletes the run files on disk. Deleting a run touches no other file, so
    /// clearing telemetry never reaches the save files.
    /// </summary>
    public interface ITelemetryRunStore
    {
        /// <summary>
        /// Lists the run identities on disk.
        /// </summary>
        /// <param name="cancellationToken">Cancels the listing.</param>
        /// <returns>The run identities, without file extensions.</returns>
        Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Reads one run's text.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>The run's text, or null when the run is absent.</returns>
        Task<string> ReadAsync(string run, CancellationToken cancellationToken);

        /// <summary>
        /// Writes an exported run to a destination file.
        /// </summary>
        /// <param name="destinationPath">The destination file path.</param>
        /// <param name="text">The exported text.</param>
        /// <param name="cancellationToken">Cancels the write.</param>
        /// <returns>A task that completes when the file is written.</returns>
        Task WriteExportAsync(string destinationPath, string text, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes one run's file.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="cancellationToken">Cancels the deletion.</param>
        /// <returns>Whether a file was deleted.</returns>
        Task<bool> DeleteAsync(string run, CancellationToken cancellationToken);
    }
}