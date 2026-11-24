using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Application.Logging;
using Iterate.Application.Onboarding;
using Iterate.Application.Persistence;
using Iterate.Application.SessionFlow;
using Iterate.Application.Telemetry;
using Iterate.Domain.Content;
using Iterate.Infrastructure.Identity;
using Iterate.Infrastructure.Logging;
using Iterate.Infrastructure.Persistence;
using Iterate.Infrastructure.Telemetry;
using SessionSurface = Iterate.Application.SessionFlow.SessionFlow;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// Plays the shipped onboarding's first tutorial to a failure and its restart, with telemetry
    /// attached over the real JSONL store in a temporary folder. Tutorial 1 needs an output of 20 to
    /// pass, and an empty source scores below it, so three executions of the untouched source fail
    /// the Process: the choice <c>ShippedOnboardingResumeTests</c> recorded. The folder is created
    /// and deleted by the suite.
    /// </summary>
    public sealed class ShippedTelemetryRunTests
    {
        private string _root;
        private SynchronizationContext _callerContext;
        private Stack _stack;

        [SetUp]
        public void SetUp()
        {
            // The launch service keeps the caller's context, and the Editor runs tests on its main
            // thread, so blocking on a launch whose file I/O completes on the pool would deadlock. The
            // suite runs with no context and restores the caller's in TearDown.
            _callerContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _root = Path.Combine(Path.GetTempPath(), $"iterate-telemetry-run-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            _stack = new Stack(_root, Catalog());
        }

        [TearDown]
        public void TearDown()
        {
            _stack.Recorder.Dispose();
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);

            SynchronizationContext.SetSynchronizationContext(_callerContext);
        }

        [Test]
        public void TutorialOneFailedAndRestarted_IsRecordedInOrderOverTheRealStore()
        {
            Assert.IsTrue(_stack.Launch.BeginAsync(CancellationToken.None).GetAwaiter().GetResult().Succeeded);
            for (int execution = 1; execution < 3; execution++)
            {
                Assert.IsTrue(_stack.Commands.Execute(new CompileAndExecute()).Accepted);
                Assert.IsTrue(_stack.Commands.Execute(new ReturnToBuild()).Accepted);
            }

            Assert.AreEqual(SessionStage.Build, _stack.Commands.Execute(new CompileAndExecute()).Stage);
            _stack.Onboarding.PendingWork.GetAwaiter().GetResult();

            IReadOnlyList<string> runs = _stack.Store.ListAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual(_stack.Recorder.ActiveRun, runs[0]);
            string text = _stack.Store.ReadAsync(runs[0], CancellationToken.None).GetAwaiter().GetResult();
            List<JsonObject> lines = Parse(text);
            List<string> types = new();
            foreach (JsonObject line in lines)
                types.Add(Text(line, "type"));

            CollectionAssert.AreEqual(new[] { TelemetryEventTypes.RunStarted, TelemetryEventTypes.ProcessEntered, TelemetryEventTypes.Configuration }, types.GetRange(0, 3));
            CollectionAssert.Contains(types, TelemetryEventTypes.Compilation);
            CollectionAssert.Contains(types, TelemetryEventTypes.ExecutionCompleted);
            int failed = types.IndexOf(TelemetryEventTypes.ProcessFailed);
            Assert.Greater(failed, 0);
            Assert.AreEqual(1, Number(Data(lines[failed]), "consecutiveFailures"));
            Assert.Greater(types.LastIndexOf(TelemetryEventTypes.ProcessEntered), failed, "The restart entered the Process again.");
            Assert.AreEqual("WB-PROC-001", Text(lines[failed], "process"));
            StringAssert.DoesNotContain("seed", text.ToLowerInvariant());
            for (int index = 0; index < lines.Count; index++)
                Assert.AreEqual(index, Number(lines[index], "seq"));
        }

        /// <summary>
        /// Parses every line of a run with the catalog reader.
        /// </summary>
        /// <param name="text">The run's text.</param>
        /// <returns>One object per line.</returns>
        private static List<JsonObject> Parse(string text)
        {
            CatalogJsonReader reader = new();
            List<JsonObject> lines = new();
            foreach (string line in text.TrimEnd('\n').Split('\n'))
                lines.Add((JsonObject)reader.Parse(line));

            return lines;
        }

        /// <summary>
        /// A string member, or null.
        /// </summary>
        /// <param name="source">The object.</param>
        /// <param name="key">The member.</param>
        /// <returns>The string, or null.</returns>
        private static string Text(JsonObject source, string key)
        {
            return source.TryGet(key, out JsonValue value) && value is JsonString text ? text.Value : null;
        }

        /// <summary>
        /// An integer member.
        /// </summary>
        /// <param name="source">The object.</param>
        /// <param name="key">The member.</param>
        /// <returns>The integer.</returns>
        private static long Number(JsonObject source, string key)
        {
            source.TryGet(key, out JsonValue value);
            return ((JsonNumber)value).IntegerValue;
        }

        /// <summary>
        /// A line's data object.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <returns>Its data.</returns>
        private static JsonObject Data(JsonObject line)
        {
            line.TryGet("data", out JsonValue data);
            return (JsonObject)data;
        }

        /// <summary>
        /// Loads the shipped catalog.
        /// </summary>
        /// <returns>The frozen catalog.</returns>
        private static ContentCatalog Catalog()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Catalog");
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), new CatalogDirectorySource(root));
            return loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// The game's stack with telemetry attached as Composition attaches it, over a save folder
        /// and a telemetry folder inside it.
        /// </summary>
        private sealed class Stack
        {
            /// <summary>
            /// The onboarding lifecycle.
            /// </summary>
            public OnboardingLifecycle Onboarding { get; }

            /// <summary>
            /// The launch service.
            /// </summary>
            public LaunchService Launch { get; }

            /// <summary>
            /// The guarded command port.
            /// </summary>
            public TelemetryCommandGuard Commands { get; }

            /// <summary>
            /// The recorder.
            /// </summary>
            public TelemetryRecorder Recorder { get; }

            /// <summary>
            /// The JSONL run store.
            /// </summary>
            public JsonlTelemetryStore Store { get; }

            /// <summary>
            /// Builds the stack.
            /// </summary>
            /// <param name="root">The save folder.</param>
            /// <param name="catalog">The shipped catalog.</param>
            public Stack(string root, ContentCatalog catalog)
            {
                GameLoggerFactory loggers = new(new ConsoleLogSink(), new LogFilterConfig(LogLevel.Warning));
                PersistenceService persistence = new(new FileSaveStore(root), loggers);
                CatalogHolder holder = new();
                holder.MarkLoaded(catalog);
                Onboarding = new OnboardingLifecycle(persistence, holder, loggers);
                SessionLifecycleRouter router = new(Onboarding, new CanonicalSessionLifecycle());
                TelemetryLifecycleTap lifecycle = new(router);
                TelemetryCommandTap commands = new();
                AgreementScopeFactory scopes = new();
                SessionSurface flow = new(
                    holder,
                    lifecycle,
                    scopes,
                    scopes,
                    Array.Empty<ICommandGate>(),
                    new ICommandObserver[] { commands }
                );
                Launch = new LaunchService(
                    persistence,
                    holder,
                    flow,
                    new SessionQueryService(flow),
                    router,
                    Onboarding,
                    new GuidSessionIdentitySource()
                );
                Store = new JsonlTelemetryStore(Path.Combine(root, "telemetry"), loggers);
                Recorder = new TelemetryRecorder(
                    commands,
                    lifecycle,
                    flow,
                    flow,
                    router,
                    Onboarding,
                    Launch,
                    Store,
                    new SystemTelemetryClock(),
                    new GuidTelemetryRunIdentitySource(),
                    new TelemetryBuildInfo("0.0.0-test", "0.1"),
                    loggers
                );
                Recorder.Initialize();
                Commands = new TelemetryCommandGuard(flow, Recorder);
            }
        }
    }
}
