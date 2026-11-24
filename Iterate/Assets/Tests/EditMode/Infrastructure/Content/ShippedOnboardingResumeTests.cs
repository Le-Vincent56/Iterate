using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Application.Logging;
using Iterate.Application.Onboarding;
using Iterate.Application.Persistence;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Infrastructure.Logging;
using Iterate.Infrastructure.Persistence;
using SessionSurface = Iterate.Application.SessionFlow.SessionFlow;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// Plays the shipped onboarding's first tutorial to a failure, quits, and relaunches over the same
    /// save folder with the real file store. The relaunch must resume at Tutorial 1 with the failure
    /// counted, and its saved checkpoint must be the document that was on disk.
    ///
    /// Tutorial 1 needs an output of 20 to pass (processes.json), and an empty source scores below it,
    /// so three executions of the untouched source fail the Process. The save folder is a fresh
    /// temporary directory, created and deleted by the suite.
    /// </summary>
    public sealed class ShippedOnboardingResumeTests
    {
        private string _root;
        private ContentCatalog _catalog;
        private SynchronizationContext _callerContext;

        [SetUp]
        public void SetUp()
        {
            // LaunchService keeps the caller's context, and the Editor runs tests on its main thread, so
            // blocking on a launch whose real file I/O completes on the pool would deadlock. The suite
            // runs with no context and restores the caller's in TearDown.
            _callerContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _root = Path.Combine(Path.GetTempPath(), $"iterate-onboarding-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            _catalog = Catalog();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);

            SynchronizationContext.SetSynchronizationContext(_callerContext);
        }

        [Test]
        public void TutorialOneFailedThenRelaunched_ResumesAtTheTutorialWithTheFailureCounted()
        {
            Launch first = new(_root, _catalog);
            Assert.IsTrue(first.Service.BeginAsync(CancellationToken.None).GetAwaiter().GetResult().Succeeded);
            Assert.AreEqual(SessionStage.Build, first.Queries.Stage);
            for (int execution = 1; execution < 3; execution++)
            {
                Assert.IsTrue(first.Flow.Execute(new CompileAndExecute()).Accepted);
                Assert.IsTrue(first.Flow.Execute(new ReturnToBuild()).Accepted, $"Execution {execution} of an empty source must stay below Pass.");
            }

            CommandResult failed = first.Flow.Execute(new CompileAndExecute());
            Assert.AreEqual(SessionStage.Build, failed.Stage, "The failed tutorial restarts.");
            first.Onboarding.PendingWork.GetAwaiter().GetResult();
            string onDisk = File.ReadAllText(Path.Combine(_root, SaveFiles.Checkpoint));

            Launch second = new(_root, _catalog);
            LaunchDecision decision = second.Service.DecideAsync(CancellationToken.None).GetAwaiter().GetResult();
            SessionBeginResult resumed = second.Service.BeginAsync(CancellationToken.None).GetAwaiter().GetResult();
            second.Onboarding.PendingWork.GetAwaiter().GetResult();

            Assert.IsTrue(decision.ResumesAtCheckpoint);
            Assert.IsTrue(resumed.Succeeded);
            Assert.AreEqual(SessionStage.Build, second.Queries.Stage);
            Assert.AreEqual("WB-PROC-001", second.Queries.Process().ProcessIdentity);
            Assert.AreEqual(1, second.Onboarding.ConsecutiveFailures);
            Assert.AreEqual(onDisk, File.ReadAllText(Path.Combine(_root, SaveFiles.Checkpoint)), "Re-entering the resumed checkpoint saves the same document.");
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
        /// One launch of the game over a save folder: the real file store, persistence, both
        /// lifecycles behind the router, Session Flow and the launch service.
        /// </summary>
        private sealed class Launch
        {
            /// <summary>
            /// The onboarding lifecycle.
            /// </summary>
            public OnboardingLifecycle Onboarding { get; }

            /// <summary>
            /// The Session surface.
            /// </summary>
            public SessionSurface Flow { get; }

            /// <summary>
            /// The queries over the same Session.
            /// </summary>
            public SessionQueryService Queries { get; }

            /// <summary>
            /// The launch service.
            /// </summary>
            public LaunchService Service { get; }

            /// <summary>
            /// Builds one launch.
            /// </summary>
            /// <param name="root">The save folder.</param>
            /// <param name="catalog">The shipped catalog.</param>
            public Launch(string root, ContentCatalog catalog)
            {
                GameLoggerFactory loggers = new(new ConsoleLogSink(), new LogFilterConfig(LogLevel.Warning));
                PersistenceService persistence = new(new FileSaveStore(root), loggers);
                CatalogHolder holder = new();
                holder.MarkLoaded(catalog);
                Onboarding = new OnboardingLifecycle(persistence, holder, loggers);
                SessionLifecycleRouter router = new(Onboarding, new CanonicalSessionLifecycle());
                AgreementScopeFactory scopes = new();
                Flow = new SessionSurface(
                    holder,
                    router,
                    scopes,
                    scopes,
                    Array.Empty<ICommandGate>(),
                    Array.Empty<ICommandObserver>()
                );
                Queries = new SessionQueryService(Flow);
                Service = new LaunchService(
                    persistence,
                    holder,
                    Flow,
                    Queries,
                    router,
                    Onboarding,
                    new Iterate.Infrastructure.Identity.GuidSessionIdentitySource()
                );
            }
        }
    }
}
