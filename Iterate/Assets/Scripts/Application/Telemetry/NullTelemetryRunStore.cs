using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// A run store with no runs, registered in release builds: it lists nothing, reads nothing,
    /// exports nothing and deletes nothing.
    /// </summary>
    public sealed class NullTelemetryRunStore : ITelemetryRunStore
    {
        /// <summary>
        /// Lists no runs.
        /// </summary>
        /// <param name="cancellationToken">Unused.</param>
        /// <returns>An empty list.</returns>
        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        /// <summary>
        /// Reads nothing.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="cancellationToken">Unused.</param>
        /// <returns>Null, as for an absent run.</returns>
        public Task<string> ReadAsync(string run, CancellationToken cancellationToken)
        {
            return Task.FromResult<string>(null);
        }

        /// <summary>
        /// Writes nothing.
        /// </summary>
        /// <param name="destinationPath">The destination file path.</param>
        /// <param name="text">The exported text.</param>
        /// <param name="cancellationToken">Unused.</param>
        /// <returns>A completed task.</returns>
        public Task WriteExportAsync(string destinationPath, string text, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Deletes nothing.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="cancellationToken">Unused.</param>
        /// <returns>False.</returns>
        public Task<bool> DeleteAsync(string run, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
    }
}