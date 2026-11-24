using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Content.Json;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Lists, exports and clears telemetry runs (UX-VAL-027, UX-VAL-028, UX-VAL-029). A run's
    /// termination is read from its last complete line; a last line without its newline was cut
    /// short and is ignored. The recorder's active run is never deleted.
    /// </summary>
    public sealed class TelemetryRunService
    {
        private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        private readonly ITelemetryRunStore _store;
        private readonly TelemetryRecorder _recorder;
        private readonly ITelemetryClock _clock;
        private readonly CatalogJsonReader _reader = new();

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="store">The run store.</param>
        /// <param name="recorder">The recorder, for its active run.</param>
        /// <param name="clock">The clock exports are stamped by.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public TelemetryRunService(ITelemetryRunStore store, TelemetryRecorder recorder, ITelemetryClock clock)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>
        /// Lists every run on disk with its start, mode and termination.
        /// </summary>
        /// <param name="cancellationToken">Cancels the reads.</param>
        /// <returns>One summary per run.</returns>
        public async Task<IReadOnlyList<TelemetryRunSummary>> ListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<string> runs = await _store.ListAsync(cancellationToken).ConfigureAwait(false);
            List<TelemetryRunSummary> summaries = new(runs.Count);
            for (int index = 0; index < runs.Count; index++)
            {
                string text = await _store.ReadAsync(runs[index], cancellationToken).ConfigureAwait(false);
                summaries.Add(Summarize(runs[index], CompleteLines(text)));
            }

            return summaries;
        }

        /// <summary>
        /// Exports runs to <c>destination/run.jsonl</c>: each run's complete lines, unchanged, and one
        /// <c>export</c> trailer stating the export time, the termination, and whether the run is active.
        /// </summary>
        /// <param name="runs">The runs to export.</param>
        /// <param name="destinationDirectory">The directory to write into.</param>
        /// <param name="cancellationToken">Cancels the reads and writes.</param>
        /// <returns>A task that completes when every run is written.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public async Task ExportAsync(IReadOnlyList<string> runs, string destinationDirectory, CancellationToken cancellationToken)
        {
            if (runs == null)
                throw new ArgumentNullException(nameof(runs));

            if (destinationDirectory == null)
                throw new ArgumentNullException(nameof(destinationDirectory));

            string active = _recorder.ActiveRun;
            for (int index = 0; index < runs.Count; index++)
            {
                string run = runs[index];
                string text = await _store.ReadAsync(run, cancellationToken).ConfigureAwait(false);
                List<string> lines = CompleteLines(text);
                TelemetryRunSummary summary = Summarize(run, lines);
                JsonObject data = new JsonObjectBuilder()
                    .Add("termination", LabelOf(summary.Termination))
                    .Add("active", run == active)
                    .Build();
                TelemetryEvent trailer = new(
                    run,
                    lines.Count,
                    TelemetryEventTypes.Export,
                    _clock.UtcNow,
                    0L,
                    null,
                    null,
                    null,
                    summary.Mode,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    data
                );
                string exported = $"{string.Concat(lines)}{TelemetryEventCodec.Encode(trailer)}";
                await _store.WriteExportAsync(Path.Combine(destinationDirectory, $"{run}.jsonl"), exported, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Deletes the named runs, refusing the active run. No other file is touched.
        /// </summary>
        /// <param name="runs">The runs to delete.</param>
        /// <param name="cancellationToken">Cancels the deletions.</param>
        /// <returns>The runs deleted and the runs refused.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the run list is null.</exception>
        public async Task<TelemetryClearResult> ClearAsync(IReadOnlyList<string> runs, CancellationToken cancellationToken)
        {
            if (runs == null)
                throw new ArgumentNullException(nameof(runs));

            List<string> deleted = new();
            List<string> refused = new();
            for (int index = 0; index < runs.Count; index++)
            {
                string run = runs[index];
                if (run == _recorder.ActiveRun)
                {
                    refused.Add(run);
                    continue;
                }

                if (await _store.DeleteAsync(run, cancellationToken).ConfigureAwait(false))
                    deleted.Add(run);
                else
                    refused.Add(run);
            }

            return new TelemetryClearResult(deleted, refused);
        }

        /// <summary>
        /// Summarizes a run from its complete lines.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="lines">Its complete lines.</param>
        /// <returns>The summary.</returns>
        private TelemetryRunSummary Summarize(string run, List<string> lines)
        {
            if (lines.Count == 0)
                return new TelemetryRunSummary(run, default, null, TelemetryTermination.ClosedWithoutEnd);

            JsonObject first = (JsonObject)_reader.Parse(lines[0]);
            JsonObject last = (JsonObject)_reader.Parse(lines[lines.Count - 1]);
            DateTimeOffset started = DateTimeOffset.ParseExact(
                StringOf(first, "at"),
                TimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal
            );
            return new TelemetryRunSummary(run, started, StringOf(first, "mode"), TerminationOf(last));
        }

        /// <summary>
        /// The termination a run's last line states.
        /// </summary>
        /// <param name="last">The last complete line.</param>
        /// <returns>The mapped termination, or closed without end when it is not <c>run_ended</c>.</returns>
        private static TelemetryTermination TerminationOf(JsonObject last)
        {
            if (StringOf(last, "type") != TelemetryEventTypes.RunEnded || !last.TryGet("data", out JsonValue data) || !(data is JsonObject members))
                return TelemetryTermination.ClosedWithoutEnd;

            return StringOf(members, "termination") switch
            {
                "completed" => TelemetryTermination.Completed,
                "failed" => TelemetryTermination.Failed,
                "abandoned" => TelemetryTermination.Abandoned,
                "error" => TelemetryTermination.Error,
                _ => TelemetryTermination.ClosedWithoutEnd,
            };
        }

        /// <summary>
        /// The label a termination is exported as.
        /// </summary>
        /// <param name="termination">The termination.</param>
        /// <returns>The label.</returns>
        private static string LabelOf(TelemetryTermination termination)
        {
            return termination switch
            {
                TelemetryTermination.Completed => "completed",
                TelemetryTermination.Failed => "failed",
                TelemetryTermination.Abandoned => "abandoned",
                TelemetryTermination.Error => "error",
                _ => "closed_without_end",
            };
        }

        /// <summary>
        /// A string member, or null when absent or not a string.
        /// </summary>
        /// <param name="source">The object.</param>
        /// <param name="key">The member.</param>
        /// <returns>The string, or null.</returns>
        private static string StringOf(JsonObject source, string key)
        {
            return source.TryGet(key, out JsonValue value) && value is JsonString text ? text.Value : null;
        }

        /// <summary>
        /// Splits a run's text into its complete lines, each keeping its newline. Text after the last
        /// newline was cut short and is dropped.
        /// </summary>
        /// <param name="text">The run's text, or null.</param>
        /// <returns>The complete lines.</returns>
        private static List<string> CompleteLines(string text)
        {
            List<string> lines = new();
            if (string.IsNullOrEmpty(text))
                return lines;

            int start = 0;
            int end = text.IndexOf('\n', start);
            while (end >= 0)
            {
                if (end > start)
                    lines.Add(text.Substring(start, end - start + 1));

                start = end + 1;
                end = text.IndexOf('\n', start);
            }

            return lines;
        }
    }
}