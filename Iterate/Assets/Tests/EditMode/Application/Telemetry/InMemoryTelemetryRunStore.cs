using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Telemetry;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// A run store over dictionaries: run texts by identity, and exported texts by destination path.
    /// </summary>
    public sealed class InMemoryTelemetryRunStore : ITelemetryRunStore
    {
        /// <summary>
        /// The run texts by run identity.
        /// </summary>
        public Dictionary<string, string> Runs { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// The exported texts by destination path.
        /// </summary>
        public Dictionary<string, string> Exports { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Lists the run identities, sorted.
        /// </summary>
        /// <param name="cancellationToken">Unused.</param>
        /// <returns>The identities.</returns>
        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
        {
            List<string> runs = new(Runs.Keys);
            runs.Sort(StringComparer.Ordinal);
            return Task.FromResult<IReadOnlyList<string>>(runs);
        }

        /// <summary>
        /// Reads a run's text.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="cancellationToken">Unused.</param>
        /// <returns>The text, or null.</returns>
        public Task<string> ReadAsync(string run, CancellationToken cancellationToken)
        {
            return Task.FromResult(Runs.TryGetValue(run, out string text) ? text : null);
        }

        /// <summary>
        /// Keeps an exported text.
        /// </summary>
        /// <param name="destinationPath">The destination path.</param>
        /// <param name="text">The text.</param>
        /// <param name="cancellationToken">Unused.</param>
        /// <returns>A completed task.</returns>
        public Task WriteExportAsync(string destinationPath, string text, CancellationToken cancellationToken)
        {
            Exports[destinationPath] = text;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Deletes a run.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="cancellationToken">Unused.</param>
        /// <returns>Whether it was present.</returns>
        public Task<bool> DeleteAsync(string run, CancellationToken cancellationToken)
        {
            return Task.FromResult(Runs.Remove(run));
        }
    }
}
