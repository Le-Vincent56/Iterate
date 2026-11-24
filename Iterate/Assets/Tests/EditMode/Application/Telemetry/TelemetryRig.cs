using System;
using System.Collections.Generic;
using System.Threading;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Application.Logging;
using Iterate.Application.Logging.Tests;
using Iterate.Application.Onboarding;
using Iterate.Application.Onboarding.Tests;
using Iterate.Application.Persistence;
using Iterate.Application.SessionFlow;
using Iterate.Application.SessionFlow.Tests;
using Iterate.Application.Telemetry;
using Iterate.Domain.Content;
using Iterate.Domain.Values;
using SessionSurface = Iterate.Application.SessionFlow.SessionFlow;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// The 6.ii onboarding stack with telemetry attached as Composition attaches it: Session Flow
    /// built over the lifecycle tap and the command tap (with a recording observer after it), the
    /// recorder initialised over a recording writer and a hand-advanced clock, and commands issued
    /// through the guard.
    /// </summary>
    public sealed class TelemetryRig
    {
        private readonly CatalogJsonReader _reader = new();

        /// <summary>
        /// The ordered log the recording fakes write to.
        /// </summary>
        public List<string> Log { get; } = new();

        /// <summary>
        /// The save file store.
        /// </summary>
        public ISaveFileStore Store { get; }

        /// <summary>
        /// The log sink.
        /// </summary>
        public FakeLogSink Sink { get; } = new();

        /// <summary>
        /// The persistence service.
        /// </summary>
        public PersistenceService Persistence { get; }

        /// <summary>
        /// The fixture catalog.
        /// </summary>
        public ContentCatalog Catalog { get; }

        /// <summary>
        /// The onboarding lifecycle.
        /// </summary>
        public OnboardingLifecycle Onboarding { get; }

        /// <summary>
        /// The lifecycle router.
        /// </summary>
        public SessionLifecycleRouter Router { get; }

        /// <summary>
        /// The lifecycle tap Session Flow calls.
        /// </summary>
        public TelemetryLifecycleTap LifecycleTap { get; }

        /// <summary>
        /// The command tap Session Flow observes through.
        /// </summary>
        public TelemetryCommandTap CommandTap { get; } = new();

        /// <summary>
        /// An observer after the tap, which must run even when the recorder faults.
        /// </summary>
        public RecordingCommandObserver After { get; }

        /// <summary>
        /// The scope factory, exposing the open Session and Process.
        /// </summary>
        public RecordingScopeFactory Scopes { get; }

        /// <summary>
        /// Session Flow.
        /// </summary>
        public SessionSurface Flow { get; }

        /// <summary>
        /// The queries over the same Session.
        /// </summary>
        public SessionQueryService Queries { get; }

        /// <summary>
        /// The ordinary Session identities.
        /// </summary>
        public FixedSessionIdentitySource Identities { get; } = new();

        /// <summary>
        /// The launch service.
        /// </summary>
        public LaunchService Launch { get; }

        /// <summary>
        /// The recording telemetry writer.
        /// </summary>
        public RecordingTelemetryWriter Writer { get; } = new();

        /// <summary>
        /// The hand-advanced clock.
        /// </summary>
        public FakeTelemetryClock Clock { get; } = new();

        /// <summary>
        /// The recorder, initialised.
        /// </summary>
        public TelemetryRecorder Recorder { get; }

        /// <summary>
        /// The guard commands are issued through.
        /// </summary>
        public TelemetryCommandGuard Commands { get; }

        /// <summary>
        /// The Process entries the lifecycle tap has reported.
        /// </summary>
        public int Entries { get; private set; }

        /// <summary>
        /// Builds the stack over a save store, optionally playing a given System first.
        /// </summary>
        /// <param name="store">The save store; a fresh fake when null.</param>
        /// <param name="firstSystem">The System to play first, or null for the fixture order.</param>
        public TelemetryRig(ISaveFileStore store = null, SystemID? firstSystem = null)
        {
            Store = store ?? new Iterate.Application.Persistence.Tests.FakeSaveFileStore();
            GameLoggerFactory loggers = new(Sink, new LogFilterConfig(LogLevel.Info));
            Persistence = new PersistenceService(Store, loggers);
            CatalogHolder holder = new();
            Catalog = firstSystem.HasValue ? OnboardingRig.PlayFirst(SessionFlowFixtures.Catalog(), firstSystem.Value) : SessionFlowFixtures.Catalog();
            holder.MarkLoaded(Catalog);
            Onboarding = new OnboardingLifecycle(Persistence, holder, loggers);
            Router = new SessionLifecycleRouter(Onboarding, new CanonicalSessionLifecycle());
            LifecycleTap = new TelemetryLifecycleTap(Router);
            LifecycleTap.ProcessEntered += entry => Entries++;
            After = new RecordingCommandObserver(Log, "after");
            Scopes = new RecordingScopeFactory(Log);
            Flow = new SessionSurface(
                holder,
                LifecycleTap,
                Scopes,
                Scopes,
                Array.Empty<ICommandGate>(),
                new ICommandObserver[] { CommandTap, After }
            );
            Queries = new SessionQueryService(Flow);
            Launch = new LaunchService(
                Persistence,
                holder,
                Flow,
                Queries,
                Router,
                Onboarding,
                Identities
            );
            Recorder = new TelemetryRecorder(
                CommandTap,
                LifecycleTap,
                Flow,
                Flow,
                Router,
                Onboarding,
                Launch,
                Writer,
                Clock,
                new FixedTelemetryRunIdentitySource(),
                new TelemetryBuildInfo("0.1.0", "0.1"),
                loggers
            );
            Recorder.Initialize();
            Commands = new TelemetryCommandGuard(Flow, Recorder);
        }

        /// <summary>
        /// Begins through the launch service.
        /// </summary>
        /// <returns>The begin result.</returns>
        public SessionBeginResult Begin()
        {
            return Launch.BeginAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Issues a command through the guard.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>Its result.</returns>
        public CommandResult Execute(ISessionCommand command)
        {
            return Commands.Execute(command);
        }

        /// <summary>
        /// Fails the open Process: executes the untouched source until its last execution, returning
        /// to Build between, then executes once more.
        /// </summary>
        /// <returns>The final execution's result.</returns>
        public CommandResult FailTheProcess()
        {
            while (Scopes.Process.Counters.ExecutionsRemaining > 1)
            {
                Execute(new CompileAndExecute());
                Execute(new ReturnToBuild());
            }

            return Execute(new CompileAndExecute());
        }

        /// <summary>
        /// A run's lines, parsed.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <returns>One object per line.</returns>
        public List<JsonObject> Lines(string run)
        {
            List<JsonObject> lines = new();
            foreach (string line in Writer.Lines[run])
                lines.Add((JsonObject)_reader.Parse(line));

            return lines;
        }

        /// <summary>
        /// A run's line types, in order.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <returns>The types.</returns>
        public List<string> Types(string run)
        {
            List<string> types = new();
            foreach (JsonObject line in Lines(run))
                types.Add(Text(line, "type"));

            return types;
        }

        /// <summary>
        /// The instance of the first Repository entry of a definition.
        /// </summary>
        /// <param name="definition">The content ID.</param>
        /// <returns>The instance.</returns>
        public InstanceID Item(string definition)
        {
            return Scopes.Session.Repository.EntriesOf(definition)[0].Item.InstanceID;
        }

        /// <summary>
        /// The Errors and Warnings logged under the telemetry category.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <returns>The count.</returns>
        public int TelemetryLogs(LogLevel level)
        {
            int count = 0;
            foreach (LogEntry entry in Sink.Entries)
            {
                if (entry.Level == level && entry.Category.Name == LogCategories.Telemetry.Name)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// A string member of a line or its data, or null.
        /// </summary>
        /// <param name="source">The object.</param>
        /// <param name="key">The member.</param>
        /// <returns>The string, or null.</returns>
        public static string Text(JsonObject source, string key)
        {
            return source.TryGet(key, out JsonValue value) && value is JsonString text ? text.Value : null;
        }

        /// <summary>
        /// An integer member of a line or its data.
        /// </summary>
        /// <param name="source">The object.</param>
        /// <param name="key">The member.</param>
        /// <returns>The integer.</returns>
        public static long Number(JsonObject source, string key)
        {
            source.TryGet(key, out JsonValue value);
            return ((JsonNumber)value).IntegerValue;
        }

        /// <summary>
        /// A Boolean member of a line or its data.
        /// </summary>
        /// <param name="source">The object.</param>
        /// <param name="key">The member.</param>
        /// <returns>The Boolean.</returns>
        public static bool Flag(JsonObject source, string key)
        {
            source.TryGet(key, out JsonValue value);
            return ((JsonBool)value).Value;
        }

        /// <summary>
        /// A line's data object.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <returns>Its data.</returns>
        public static JsonObject Data(JsonObject line)
        {
            line.TryGet("data", out JsonValue data);
            return (JsonObject)data;
        }

        /// <summary>
        /// The lines of a type, in order.
        /// </summary>
        /// <param name="lines">The lines.</param>
        /// <param name="type">The type.</param>
        /// <returns>The matching lines.</returns>
        public static List<JsonObject> OfType(List<JsonObject> lines, string type)
        {
            List<JsonObject> matching = new();
            foreach (JsonObject line in lines)
            {
                if (Text(line, "type") == type)
                    matching.Add(line);
            }

            return matching;
        }
    }
}
