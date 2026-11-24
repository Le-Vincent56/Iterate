using System;
using System.Collections.Generic;
using System.Threading;
using Iterate.Application.Content.Json;
using Iterate.Application.Logging;
using Iterate.Application.Onboarding;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Records each Session as one telemetry run, one flushed line per event (TA-TRC-006).
    ///
    /// Session Flow raises its events and calls its lifecycle inside a command but notifies its
    /// observers after it, so the recorder buffers what it hears during a command and, when the
    /// command is observed, writes the command's line first and then its consequences in arrival
    /// order. A refused command's consequences are discarded. Each consequence is stamped when it
    /// arrives, except a Process entry, which fires before its Process exists and is read when it
    /// is written. A begin is framed by its run-opening stage change instead of a command.
    ///
    /// Every handler runs inside a fault barrier: a recorder defect is logged once, recorded as a
    /// <c>telemetry_fault</c> line, and stops that run's recording, and never reaches Session Flow.
    /// The recorder is driven on Session Flow's thread; only <see cref="ActiveRun"/> is read across
    /// threads.
    /// </summary>
    public sealed class TelemetryRecorder : IDisposable
    {
        private readonly TelemetryCommandTap _commands;
        private readonly TelemetryLifecycleTap _lifecycle;
        private readonly ISessionEvents _events;
        private readonly SessionFlow.SessionFlow _flow;
        private readonly SessionLifecycleRouter _router;
        private readonly OnboardingLifecycle _onboarding;
        private readonly LaunchService _launch;
        private readonly ITelemetryWriter _writer;
        private readonly ITelemetryClock _clock;
        private readonly ITelemetryRunIdentitySource _runs;
        private readonly TelemetryBuildInfo _build;
        private readonly IGameLogger _logger;
        private readonly TelemetrySnapshotMapper _snapshots;
        private readonly CommandDataProjector _projector = new();
        private readonly List<Pending> _pending = new();
        private TelemetryRun _run;
        private string _activeRun;
        private TelemetryEvent _preCommand;
        private bool _initialized;

        /// <summary>
        /// The active run's identity, or null outside a run. Safe to read from another thread.
        /// </summary>
        public string ActiveRun => Volatile.Read(ref _activeRun);

        /// <summary>
        /// Whether <see cref="Initialize"/> has subscribed the recorder.
        /// </summary>
        public bool Initialized => _initialized;

        /// <summary>
        /// Creates the recorder. It subscribes only when <see cref="Initialize"/> is called.
        /// </summary>
        /// <param name="commands">The command tap Session Flow observes through.</param>
        /// <param name="lifecycle">The lifecycle tap Session Flow calls.</param>
        /// <param name="events">Session Flow's events.</param>
        /// <param name="flow">Session Flow, for its context.</param>
        /// <param name="router">The lifecycle router, for the Session mode.</param>
        /// <param name="onboarding">The onboarding lifecycle, for its failure count.</param>
        /// <param name="launch">The launch service, for the begin being issued.</param>
        /// <param name="writer">The run file writer.</param>
        /// <param name="clock">The telemetry clock.</param>
        /// <param name="runs">The run identity source.</param>
        /// <param name="build">The build and UX versions.</param>
        /// <param name="loggers">The logger factory.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public TelemetryRecorder(
            TelemetryCommandTap commands,
            TelemetryLifecycleTap lifecycle,
            ISessionEvents events,
            SessionFlow.SessionFlow flow,
            SessionLifecycleRouter router,
            OnboardingLifecycle onboarding,
            LaunchService launch,
            ITelemetryWriter writer,
            ITelemetryClock clock,
            ITelemetryRunIdentitySource runs,
            TelemetryBuildInfo build,
            IGameLoggerFactory loggers
        )
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _onboarding = onboarding ?? throw new ArgumentNullException(nameof(onboarding));
            _launch = launch ?? throw new ArgumentNullException(nameof(launch));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _runs = runs ?? throw new ArgumentNullException(nameof(runs));
            _build = build ?? throw new ArgumentNullException(nameof(build));
            if (loggers == null)
                throw new ArgumentNullException(nameof(loggers));

            LogCategory category = LogCategories.Telemetry;
            _logger = loggers.Create(in category);
            _snapshots = new TelemetrySnapshotMapper(flow);
        }

        /// <summary>
        /// Subscribes to the taps and Session Flow's events. Calling it again does nothing.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            _lifecycle.ProcessEntered += OnProcessEntered;
            _lifecycle.ProcessFailed += OnProcessFailed;
            _events.StageChanged += OnStageChanged;
            _events.StateChanged += OnStateChanged;
            _commands.CommandObserved += OnCommandObserved;
        }

        /// <summary>
        /// Unsubscribes, and closes an active run's file without an end line.
        /// </summary>
        public void Dispose()
        {
            if (!_initialized)
                return;

            _initialized = false;
            _lifecycle.ProcessEntered -= OnProcessEntered;
            _lifecycle.ProcessFailed -= OnProcessFailed;
            _events.StageChanged -= OnStageChanged;
            _events.StateChanged -= OnStateChanged;
            _commands.CommandObserved -= OnCommandObserved;
            if (_run != null)
                CloseRun();
        }

        /// <summary>
        /// Tells the recorder a command is starting: any buffer left behind is emptied, and the
        /// state before the command is taken. Called by the command guard.
        /// </summary>
        public void CommandStarting()
        {
            try
            {
                _pending.Clear();
                if (_run != null)
                    _preCommand = Envelope(null);
            }
            catch (Exception exception)
            {
                Contain(exception);
            }
        }

        /// <summary>
        /// Records a command that threw as an unrecoverable error (UX-VAL-029): its line with
        /// <c>result: error</c>, its buffered consequences discarded, then <c>run_ended</c> with
        /// the exception's type name and never its message. The run is then closed.
        /// </summary>
        /// <param name="command">The command that threw.</param>
        /// <param name="exception">The exception.</param>
        public void CommandThrew(ISessionCommand command, Exception exception)
        {
            try
            {
                _pending.Clear();
                if (_run == null)
                    return;

                if (!_run.Faulted)
                {
                    _run.Write(Envelope(TelemetryEventTypes.Command) with { Subject = command?.GetType().Name, Result = "error" });
                    JsonObject ended = new JsonObjectBuilder()
                        .Add("termination", "error")
                        .Add("exception", exception?.GetType().Name)
                        .Build();
                    _run.Write(Envelope(TelemetryEventTypes.RunEnded) with { Data = ended });
                }

                CloseRun();
            }
            catch (Exception contained)
            {
                Contain(contained);
            }
        }

        /// <summary>
        /// Appends a line of Presentation's own into the active run, with the full envelope. Outside
        /// a run it does nothing.
        /// </summary>
        /// <param name="type">The event label.</param>
        /// <param name="subject">The subject, or null.</param>
        /// <param name="target">The target, or null.</param>
        /// <param name="data">The type-specific data, or null for none.</param>
        public void Record(
            string type,
            string subject,
            string target,
            JsonObject data
        )
        {
            try
            {
                if (_run == null || _run.Faulted)
                    return;

                _run.Write(Envelope(type) with { Subject = subject, Target = target, Data = data ?? Empty() });
            }
            catch (Exception exception)
            {
                Contain(exception);
            }
        }

        /// <summary>
        /// Buffers a Process entry; it is written once its Process exists.
        /// </summary>
        /// <param name="entry">The entry.</param>
        private void OnProcessEntered(ProcessEntry entry)
        {
            try
            {
                if (_run == null)
                    _pending.Clear();

                _pending.Add(new Pending(null, entry, null, null));
            }
            catch (Exception exception)
            {
                Contain(exception);
            }
        }

        /// <summary>
        /// Buffers a Process failure, stamped now, before Session Flow tears the Process down.
        /// </summary>
        /// <param name="failure">The failure.</param>
        private void OnProcessFailed(ProcessFailure failure)
        {
            try
            {
                if (_run == null)
                    return;

                SessionContext context = _flow.Context;
                int failures = _router.Mode == SessionMode.Onboarding ? _onboarding.ConsecutiveFailures : _run.NoteFailure(failure.Process);
                CurrentResult result = context.Result;
                JsonObject data = new JsonObjectBuilder()
                    .Add("output", result == null ? JsonValues.Null() : JsonValues.Integer(result.Record.FinalState.FinalOutput.Value))
                    .Add("pass", context.Process == null ? JsonValues.Null() : JsonValues.Integer(context.Process.Configuration.Thresholds.Pass))
                    .Add("disposition", failure.Disposition.ToString())
                    .Add("consecutiveFailures", failures)
                    .Build();
                TelemetryEvent failed = Envelope(TelemetryEventTypes.ProcessFailed) with { Subject = failure.Process.Value, Data = data };
                _pending.Add(new Pending(failed, null, null, null));
            }
            catch (Exception exception)
            {
                Contain(exception);
            }
        }

        /// <summary>
        /// Opens a run at a begin's stage change; otherwise buffers the stage change.
        /// </summary>
        /// <param name="change">The stage change.</param>
        private void OnStageChanged(StageChange change)
        {
            try
            {
                if (change.Previous == SessionStage.NoSession || change.Previous == SessionStage.SessionEnded)
                {
                    OpenRun(change);
                    return;
                }

                if (_run != null)
                    _pending.Add(new Pending(null, null, change, null));
            }
            catch (Exception exception)
            {
                Contain(exception);
            }
        }

        /// <summary>
        /// Buffers the Buffer's state, stamped now, when a change includes the Buffer.
        /// </summary>
        /// <param name="change">The changed areas.</param>
        private void OnStateChanged(StateChange change)
        {
            try
            {
                if (_run == null || (change.Areas & ChangedAreas.Buffer) == 0)
                    return;

                BufferSnapshot buffer = _snapshots.Buffer();
                if (buffer == null)
                    return;

                JsonObject data = TelemetryJson.AddBuffer(new JsonObjectBuilder(), buffer).Build();
                _pending.Add(new Pending(Envelope(TelemetryEventTypes.BufferChanged) with { Data = data }, null, null, buffer));
            }
            catch (Exception exception)
            {
                Contain(exception);
            }
        }

        /// <summary>
        /// Writes a finished command's line and then what it caused.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        private void OnCommandObserved(CommandObservation observed)
        {
            try
            {
                WriteCommand(observed);
            }
            catch (Exception exception)
            {
                Contain(exception);
            }
        }

        /// <summary>
        /// Writes a finished command's lines: the command, a new configuration after a Build edit,
        /// its compilation and execution, then its buffered consequences, or none when refused.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        private void WriteCommand(CommandObservation observed)
        {
            if (_run == null)
            {
                _pending.Clear();
                return;
            }

            if (_run.Faulted)
            {
                _pending.Clear();
                if (observed.Result.Stage == SessionStage.SessionEnded)
                    CloseRun();

                return;
            }

            TelemetryEvent before = _preCommand ?? Envelope(null);
            TelemetryEvent after = Envelope(TelemetryEventTypes.Command);
            CommandResult result = observed.Result;
            string previousConfiguration = _run.Configuration;
            ConfigurationSnapshot snapshot = null;
            if (result.Accepted && before.Stage == nameof(SessionStage.Build) && IsBuildEdit(observed.Command))
            {
                snapshot = _snapshots.Configuration();
                _run.NoteConfiguration(snapshot);
            }

            CommandProjection projection = _projector.Project(observed, new ResourceChange(before.Resources, after.Resources));
            JsonObject data = projection.Data;
            if (!result.Accepted && result.Rejection == CommandRejection.DomainRejected && result.DomainResult != null)
                data = WithDomainResult(data, result.DomainResult);

            _run.Write(after with
            {
                Subject = observed.Command.GetType().Name,
                Target = projection.Target,
                Result = result.Accepted ? "accepted" : "refused",
                Reason = result.Accepted ? null : result.Rejection.ToString(),
                Configuration = _run.Configuration,
                Data = data,
            });

            if (_run.Configuration != previousConfiguration)
                WriteConfiguration(snapshot);

            if (observed.Command is CompileAndExecute)
                WriteCompilation(before, result);

            if (!result.Accepted)
            {
                _pending.Clear();
                _preCommand = Envelope(null);
                return;
            }

            Flush();
            _preCommand = _run == null ? null : Envelope(null);
        }

        /// <summary>
        /// Writes a <c>CompileAndExecute</c>'s compilation and, when it ran, its execution, before
        /// any failure or restart it caused. Their envelope is the state before the command, with
        /// Bytes after computed from the attempt and the execution counted.
        /// </summary>
        /// <param name="before">The envelope before the command.</param>
        /// <param name="result">The command's result.</param>
        private void WriteCompilation(TelemetryEvent before, CommandResult result)
        {
            CompilationAttempt attempt = result.DomainResult switch
            {
                CompilationAttempt blocked => blocked,
                ExecutionOutcome executed => executed.Attempt,
                _ => null,
            };
            if (attempt == null)
                return;

            JsonObject compilation = TelemetryJson.Compilation(attempt);
            int bytesAfter = attempt.Committed ? attempt.AvailableBytes.Value - attempt.Breakdown.FinalCost : attempt.AvailableBytes.Value;
            TelemetryResources resources = before.Resources == null ? null : before.Resources with { Bytes = bytesAfter };
            TelemetryEvent stamped = before with
            {
                Type = TelemetryEventTypes.Compilation,
                Subject = null,
                Target = null,
                Result = attempt.Committed ? "committed" : "blocked",
                Reason = attempt.Committed ? null : attempt.BlockReason.ToString(),
                Resources = resources,
                Configuration = _run.Configuration,
                Data = compilation,
            };
            _run.Write(stamped);
            if (!(result.DomainResult is ExecutionOutcome outcome))
                return;

            bool autoSubmitted = outcome.Evaluation.Reached.HasValue && result.Stage != SessionStage.ResultReview;
            _run.Write(stamped with
            {
                Type = TelemetryEventTypes.ExecutionCompleted,
                Execution = (before.Execution ?? 0) + 1,
                Result = outcome.Evaluation.Reached?.ToString() ?? "below_pass",
                Reason = null,
                Data = TelemetryJson.Execution(outcome, _run.Configuration, autoSubmitted),
            });
        }

        /// <summary>
        /// Writes the buffered consequences in arrival order. A Process entry is written after any
        /// stage exits the same command caused; with none, a Process entry splits the Build dwell,
        /// as an onboarding restart or a passed final execution re-enters Build without a stage change.
        /// </summary>
        private void Flush()
        {
            bool stageChanged = false;
            for (int index = 0; index < _pending.Count; index++)
            {
                if (_pending[index].Stage.HasValue)
                    stageChanged = true;
            }

            for (int index = 0; index < _pending.Count && _run != null; index++)
            {
                Pending pending = _pending[index];
                if (pending.Entry.HasValue)
                {
                    for (int later = index + 1; later < _pending.Count && _run != null; later++)
                    {
                        if (_pending[later].Stage.HasValue)
                        {
                            WriteStageChange(_pending[later].Stage.Value);
                            _pending[later] = new Pending(null, null, null, null);
                        }
                    }

                    if (_run == null)
                        break;

                    if (!stageChanged && _run.Stage == SessionStage.Build)
                        WriteStageExit(SessionStage.Build);

                    WriteProcessEntered(pending.Entry.Value);
                }
                else if (pending.Stage.HasValue)
                {
                    WriteStageChange(pending.Stage.Value);
                }
                else if (pending.Event != null)
                {
                    if (pending.Buffer != null)
                    {
                        if (pending.Buffer.Equals(_run.LastBuffer))
                            continue;

                        _run.LastBuffer = pending.Buffer;
                    }

                    _run.Write(pending.Event with { Configuration = pending.Event.Configuration ?? _run.Configuration });
                }
            }

            _pending.Clear();
        }

        /// <summary>
        /// Opens a run: its identity, its file, <c>run_started</c>, then the begin's buffered entry.
        /// </summary>
        /// <param name="change">The run-opening stage change.</param>
        private void OpenRun(StageChange change)
        {
            if (_run != null)
                CloseRun();

            SessionContext context = _flow.Context;
            string run = _runs.Next();
            _run = new TelemetryRun(run, _writer, _clock, _build, context.Session?.CatalogRevision);
            Volatile.Write(ref _activeRun, run);
            _writer.Open(run);

            LaunchDecision beginning = _launch.Beginning;
            string launch = beginning == null ? "direct" : beginning.ResumesAtCheckpoint ? "resumed" : "fresh";
            JsonObject data = new JsonObjectBuilder()
                .Add("launch", launch)
                .Add("entryProcess", context.Process?.ProcessIdentity)
                .Add("entryStage", change.Current.ToString())
                .Add("consecutiveFailures", _router.Mode == SessionMode.Onboarding ? _onboarding.ConsecutiveFailures : 0)
                .Build();
            _run.Write(Envelope(TelemetryEventTypes.RunStarted) with { Data = data });
            Flush();
            if (_run == null)
                return;

            _run.EnterStage(change.Current);
            _preCommand = Envelope(null);
        }

        /// <summary>
        /// Writes a Process entry with its Buffer state, then its configuration when it is new.
        /// </summary>
        /// <param name="entry">The entry.</param>
        private void WriteProcessEntered(ProcessEntry entry)
        {
            BufferSnapshot buffer = _snapshots.Buffer();
            _run.LastBuffer = buffer;
            string previousConfiguration = _run.Configuration;
            ConfigurationSnapshot snapshot = _snapshots.Configuration();
            _run.NoteConfiguration(snapshot);
            int failures = _router.Mode == SessionMode.Onboarding ? _onboarding.ConsecutiveFailures : _run.FailuresOf(entry.Process);
            JsonObjectBuilder builder = new JsonObjectBuilder()
                .Add("route", entry.Route?.Value)
                .Add("consecutiveFailures", failures);
            JsonObject data = TelemetryJson.AddBuffer(builder, buffer).Build();
            _run.Write(Envelope(TelemetryEventTypes.ProcessEntered) with { Subject = entry.Process.Value, Data = data });
            if (_run.Configuration != previousConfiguration)
                WriteConfiguration(snapshot);
        }

        /// <summary>
        /// Writes a stage exit, and the run's end when the Session ended.
        /// </summary>
        /// <param name="change">The stage change.</param>
        private void WriteStageChange(StageChange change)
        {
            WriteStageExit(change.Previous);
            _run.EnterStage(change.Current);
            if (change.Current != SessionStage.SessionEnded)
                return;

            JsonObject ended = new JsonObjectBuilder().Add("termination", TerminationOf(change.EndReason)).Build();
            _run.Write(Envelope(TelemetryEventTypes.RunEnded) with { Data = ended });
            CloseRun();
        }

        /// <summary>
        /// Writes the exit of a stage with its dwell.
        /// </summary>
        /// <param name="stage">The stage left.</param>
        private void WriteStageExit(SessionStage stage)
        {
            JsonObject data = new JsonObjectBuilder()
                .Add("stage", stage.ToString())
                .Add("dwell", _run.StageDwell())
                .Build();
            _run.Write(Envelope(TelemetryEventTypes.StageExited) with { Subject = stage.ToString(), Data = data });
        }

        /// <summary>
        /// Writes a configuration snapshot under its own new identity.
        /// </summary>
        /// <param name="snapshot">The snapshot.</param>
        private void WriteConfiguration(ConfigurationSnapshot snapshot)
        {
            _run.Write(Envelope(TelemetryEventTypes.Configuration) with { Data = TelemetryJson.Configuration(snapshot) });
        }

        /// <summary>
        /// Closes the active run's file and drops its state.
        /// </summary>
        private void CloseRun()
        {
            string run = _run.Run;
            _run = null;
            _preCommand = null;
            Volatile.Write(ref _activeRun, null);
            _writer.Close(run);
        }

        /// <summary>
        /// Contains a recorder defect: logs one Error for the run, writes <c>telemetry_fault</c> if
        /// the run still accepts lines, and stops the run's recording.
        /// </summary>
        /// <param name="exception">The defect.</param>
        private void Contain(Exception exception)
        {
            _pending.Clear();
            if (_run != null && _run.Faulted)
                return;

            _logger.Error($"Telemetry recording stopped after a {exception.GetType().Name}", exception);
            if (_run == null)
                return;

            try
            {
                JsonObject data = new JsonObjectBuilder().Add("exception", exception.GetType().Name).Build();
                _run.Write(Envelope(TelemetryEventTypes.TelemetryFault) with { Data = data });
            }
            catch (Exception)
            {
            }

            _run.Fault();
        }

        /// <summary>
        /// The envelope as Session Flow's context stands now.
        /// </summary>
        /// <param name="type">The event label.</param>
        /// <returns>An event with the envelope set and nothing else.</returns>
        private TelemetryEvent Envelope(string type)
        {
            SessionContext context = _flow.Context;
            ProcessState process = context.Process;
            return new TelemetryEvent(
                null,
                0,
                type,
                default,
                0L,
                null,
                null,
                null,
                _router.Mode == SessionMode.Onboarding ? "onboarding" : "ordinary",
                context.Stage.ToString(),
                process?.ProcessIdentity,
                process?.Counters.ExecutionsRun,
                null,
                null,
                null,
                null,
                ResourcesOf(context),
                _run?.Configuration,
                Empty()
            );
        }

        /// <summary>
        /// The resources as Session Flow's context stands now.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The resources, or null outside a Session.</returns>
        private static TelemetryResources ResourcesOf(SessionContext context)
        {
            SessionState session = context.Session;
            if (session == null)
                return null;

            DependencyRack rack = session.Economy.Dependencies;
            return new TelemetryResources(
                context.Process?.Bytes.Balance.Value ?? 0,
                session.Economy.Tokens.Balance.Value,
                rack.Usage,
                rack.Capacity
            );
        }

        /// <summary>
        /// Whether a command edits the source arrangement or the Buffer during Build.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>Whether a configuration snapshot is taken after it.</returns>
        private static bool IsBuildEdit(ISessionCommand command)
        {
            return command is InstallLine || command is RemoveLine || command is MoveLine || command is SwapLines || command is ArchiveItem || command is ConfirmDirective;
        }

        /// <summary>
        /// Adds a Domain refusal's result type to a refused command's data.
        /// </summary>
        /// <param name="data">The command's data.</param>
        /// <param name="domainResult">The Domain result.</param>
        /// <returns>The data with <c>domainResult</c> appended.</returns>
        private static JsonObject WithDomainResult(JsonObject data, object domainResult)
        {
            JsonObjectBuilder builder = new();
            for (int index = 0; index < data.Keys.Count; index++)
            {
                string key = data.Keys[index];
                data.TryGet(key, out JsonValue value);
                builder.Add(key, value);
            }

            return builder.Add("domainResult", domainResult.GetType().Name).Build();
        }

        /// <summary>
        /// The termination label of a Session end reason.
        /// </summary>
        /// <param name="reason">The end reason.</param>
        /// <returns><c>completed</c>, <c>failed</c> or <c>abandoned</c>.</returns>
        private static string TerminationOf(SessionEndReason reason)
        {
            return reason switch
            {
                SessionEndReason.Completed => "completed",
                SessionEndReason.Failed => "failed",
                SessionEndReason.Abandoned => "abandoned",
                _ => "unknown",
            };
        }

        /// <summary>
        /// An empty data object.
        /// </summary>
        /// <returns>The object.</returns>
        private static JsonObject Empty()
        {
            return new JsonObjectBuilder().Build();
        }

        /// <summary>
        /// One buffered consequence: a stamped event (with the Buffer state it carries, if any), a
        /// Process entry, or a stage change.
        /// </summary>
        /// <param name="Event">The stamped event, or null.</param>
        /// <param name="Entry">The Process entry, or null.</param>
        /// <param name="Stage">The stage change, or null.</param>
        /// <param name="Buffer">The Buffer state a <c>buffer_changed</c> event carries, or null.</param>
        private sealed record Pending(
            TelemetryEvent Event,
            ProcessEntry? Entry,
            StageChange? Stage,
            BufferSnapshot Buffer
        );
    }
}