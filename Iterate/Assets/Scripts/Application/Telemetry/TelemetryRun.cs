using System;
using System.Collections.Generic;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Values;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// One telemetry run's state: its identity, its line sequence, its configuration snapshots, its
    /// stage dwell, its failure counts and whether it has faulted. The recorder holds one while a
    /// Session runs and drops it when the run closes, so the next run starts clean.
    /// </summary>
    public sealed class TelemetryRun
    {
        private readonly ITelemetryWriter _writer;
        private readonly ITelemetryClock _clock;
        private readonly TelemetryBuildInfo _build;
        private readonly string _content;
        private readonly long _started;
        private readonly Dictionary<ProcessID, int> _failures = new();
        private int _sequence;
        private int _configurations;
        private ConfigurationSnapshot _lastConfiguration;
        private long _stageStamp;

        /// <summary>
        /// The run identity.
        /// </summary>
        public string Run { get; }

        /// <summary>
        /// Whether the run has faulted; a faulted run writes nothing more.
        /// </summary>
        public bool Faulted { get; private set; }

        /// <summary>
        /// The current configuration snapshot identity, or null before the first.
        /// </summary>
        public string Configuration { get; private set; }

        /// <summary>
        /// The stage the run's dwell is being measured in, or null before the first.
        /// </summary>
        public SessionStage? Stage { get; private set; }

        /// <summary>
        /// The Buffer state last written, so an unchanged Buffer is not written again.
        /// </summary>
        public BufferSnapshot LastBuffer { get; set; }

        /// <summary>
        /// Opens a run's state; the caller opens its file.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="writer">The writer lines are appended through.</param>
        /// <param name="clock">The clock lines are stamped by.</param>
        /// <param name="build">The build and UX versions.</param>
        /// <param name="content">The catalog revision.</param>
        /// <exception cref="ArgumentNullException">Thrown when the run, writer, clock or build is null.</exception>
        public TelemetryRun(
            string run,
            ITelemetryWriter writer,
            ITelemetryClock clock,
            TelemetryBuildInfo build,
            string content
        )
        {
            Run = run ?? throw new ArgumentNullException(nameof(run));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _content = content;
            _started = clock.ElapsedMilliseconds;
            _stageStamp = _started;
        }

        /// <summary>
        /// Stamps a partial event with the run, sequence, times and versions, and appends it. A
        /// faulted run drops it. The sequence advances only once the line is appended.
        /// </summary>
        /// <param name="partial">The event, its run-level members unset.</param>
        public void Write(TelemetryEvent partial)
        {
            if (Faulted)
                return;

            TelemetryEvent stamped = partial with
            {
                Run = Run,
                Sequence = _sequence,
                At = _clock.UtcNow,
                Milliseconds = _clock.ElapsedMilliseconds - _started,
                Build = _build.BuildVersion,
                UX = _build.UXVersion,
                Content = _content,
            };
            _writer.Append(Run, TelemetryEventCodec.Encode(stamped));
            _sequence++;
        }

        /// <summary>
        /// Notes a configuration snapshot, assigning a new identity only when it differs from the last.
        /// </summary>
        /// <param name="snapshot">The snapshot, or null when no Process is open.</param>
        /// <returns>The current configuration identity, <c>cfg-n</c>, or null before the first.</returns>
        public string NoteConfiguration(ConfigurationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Equals(_lastConfiguration))
                return Configuration;

            _configurations++;
            _lastConfiguration = snapshot;
            Configuration = $"cfg-{_configurations}";
            return Configuration;
        }

        /// <summary>
        /// Measures the dwell since the last stage stamp, and restamps.
        /// </summary>
        /// <returns>The dwell in milliseconds.</returns>
        public long StageDwell()
        {
            long now = _clock.ElapsedMilliseconds;
            long dwell = now - _stageStamp;
            _stageStamp = now;
            return dwell;
        }

        /// <summary>
        /// Records the stage whose dwell is now being measured.
        /// </summary>
        /// <param name="stage">The stage entered.</param>
        public void EnterStage(SessionStage stage)
        {
            Stage = stage;
        }

        /// <summary>
        /// Counts an ordinary-mode failure of a Process.
        /// </summary>
        /// <param name="process">The Process that failed.</param>
        /// <returns>The run's failures of that Process so far.</returns>
        public int NoteFailure(ProcessID process)
        {
            int count = FailuresOf(process) + 1;
            _failures[process] = count;
            return count;
        }

        /// <summary>
        /// The run's failures of a Process so far.
        /// </summary>
        /// <param name="process">The Process.</param>
        /// <returns>The count, zero when it has not failed.</returns>
        public int FailuresOf(ProcessID process)
        {
            return _failures.TryGetValue(process, out int count) ? count : 0;
        }

        /// <summary>
        /// Marks the run faulted; it writes nothing more.
        /// </summary>
        public void Fault()
        {
            Faulted = true;
        }
    }
}