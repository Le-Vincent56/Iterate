using System;
using System.Threading;
using NUnit.Framework;
using Didionysymus.Lattice.Runtime;
using Iterate.Application.Content;
using Iterate.Application.Logging;
using Iterate.Application.Onboarding;
using Iterate.Application.Persistence;
using Iterate.Application.SessionFlow;
using Iterate.Application.Telemetry;
using Iterate.Composition.Root;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;
using Iterate.Infrastructure.Logging;
using Iterate.Infrastructure.Persistence;
using Iterate.Infrastructure.Telemetry;

namespace Iterate.Composition.Root.Tests
{
    /// <summary>
    /// Headless composition tests: the project installer must yield a container that resolves
    /// the logging seam, the persistence seam and the Session surface without any scene, and the
    /// Session and Process scope factories must open and close real nested scopes.
    /// </summary>
    public sealed class ProjectCompositionTests
    {
        [Test]
        public void Build_WithProjectInstaller_ResolvesLoggerFactory()
        {
            // Arrange
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            // Act
            IGameLoggerFactory factory = resolver.Resolve<IGameLoggerFactory>();
            LogCategory category = new("Test");
            IGameLogger logger = factory.Create(in category);

            // Assert
            Assert.IsNotNull(logger);
            Assert.AreEqual(category, logger.Category);

            resolver.Dispose();
        }

        [Test]
        public void Build_WithProjectInstaller_SinkIsConsoleSink()
        {
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            ILogSink sink = resolver.Resolve<ILogSink>();

            Assert.IsInstanceOf<ConsoleLogSink>(sink);

            resolver.Dispose();
        }

        [Test]
        public void Build_WithProjectInstaller_ResolvesPersistenceService()
        {
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            PersistenceService service = resolver.Resolve<PersistenceService>();

            Assert.IsNotNull(service);

            resolver.Dispose();
        }

        [Test]
        public void Build_WithProjectInstaller_SaveStoreIsFileSaveStore()
        {
            // The adapter is bound by factory over the platform persistent-data path, which only exists
            // inside the player — so this is the one place the real binding can be proved at all.
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            ISaveFileStore store = resolver.Resolve<ISaveFileStore>();

            Assert.IsInstanceOf<FileSaveStore>(store);

            resolver.Dispose();
        }

        [Test]
        public void Build_WithProjectInstaller_TheCommandPortIsTheGuardOverTheOneSessionFlow()
        {
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            ISessionCommands commands = resolver.Resolve<ISessionCommands>();
            ISessionEvents events = resolver.Resolve<ISessionEvents>();

            Assert.IsInstanceOf<TelemetryCommandGuard>(commands);
            Assert.AreSame(resolver.Resolve<SessionFlow>(), ((TelemetryCommandGuard)commands).Inner);
            Assert.AreSame(commands, resolver.Resolve<ISessionCommands>());
            Assert.AreSame(resolver.Resolve<SessionFlow>(), events);
            resolver.Dispose();
        }

        [Test]
        public void Build_WithProjectInstaller_QueriesReadTheSessionFlowSession()
        {
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));
            ContentCatalog catalog = LoadShippedCatalog(resolver);
            SessionFlow flow = resolver.Resolve<SessionFlow>();
            ISessionQueries queries = resolver.Resolve<ISessionQueries>();

            SessionBeginResult begun = flow.BeginSession(catalog.StarterArchetypes[0], new SystemID("WB-SYS-001"), "session-1", "seed-1");

            Assert.IsTrue(begun.Succeeded);
            Assert.AreEqual(SessionStage.Build, queries.Stage);
            Assert.AreEqual("WB-PROC-001", queries.Process().ProcessIdentity);
            flow.Execute(new AbandonSession());
            Assert.AreEqual(SessionStage.SessionEnded, queries.Stage);
            resolver.Dispose();
        }

        [Test]
        public void ScopeFactories_OpenNestedScopesThatHoldTheirHoldersUntilDisposed()
        {
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));
            ContentCatalog catalog = LoadShippedCatalog(resolver);
            SessionState session = SessionState.Create(catalog, catalog.StarterArchetypes[0], "WB-SYS-001", "session-1", "seed-1");
            Assert.IsTrue(catalog.TryGetProcessConfiguration(new ProcessID("WB-PROC-001"), out ProcessConfigurationDefinition configuration));
            ProcessSetup setup = ProcessSetupResolver.Resolve(configuration, catalog.Parameters, session.Economy.SetupEffectsFor(configuration.ID));
            ProcessState process = ProcessState.Create(configuration, setup, catalog, session, null).State;
            BuildState build = new(process.InitialArrangement, process.Buffer, catalog.Parameters);

            IDisposable sessionScope = resolver.Resolve<ISessionScopeFactory>().Open(session);
            IDisposable processScope = resolver.Resolve<IProcessScopeFactory>().Open(sessionScope, process, build);
            IObjectResolver processResolver = (IObjectResolver)processScope;
            IObjectResolver sessionResolver = (IObjectResolver)sessionScope;

            Assert.AreSame(session, sessionResolver.Resolve<SessionState>());
            Assert.AreSame(process, processResolver.Resolve<ProcessState>());
            Assert.AreSame(build, processResolver.Resolve<BuildState>());
            Assert.AreSame(session, processResolver.Resolve<SessionState>(), "A Process scope sees its Session's holder.");

            processScope.Dispose();
            Assert.Throws<ObjectDisposedException>(() => processResolver.Resolve<ProcessState>());
            Assert.AreSame(session, sessionResolver.Resolve<SessionState>(), "Closing the Process scope leaves the Session scope open.");

            sessionScope.Dispose();
            Assert.Throws<ObjectDisposedException>(() => sessionResolver.Resolve<SessionState>());
            resolver.Dispose();
        }

        [Test]
        public void Build_WithProjectInstaller_TheLifecycleIsTheTelemetryTapOverTheRouter()
        {
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            ISessionLifecycle lifecycle = resolver.Resolve<ISessionLifecycle>();

            Assert.AreSame(resolver.Resolve<TelemetryLifecycleTap>(), lifecycle);
            SessionLifecycleRouter router = resolver.Resolve<SessionLifecycleRouter>();
            Assert.AreSame(router, ((TelemetryLifecycleTap)lifecycle).Inner);
            Assert.AreEqual(SessionMode.Ordinary, router.Mode);
            resolver.Dispose();
        }

        [Test]
        public void Build_WithProjectInstaller_SessionFlowObservesThroughTheCommandTap()
        {
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));
            int observed = 0;
            resolver.Resolve<TelemetryCommandTap>().CommandObserved += observation => observed++;

            resolver.Resolve<SessionFlow>().Execute(new AbandonSession());

            Assert.AreEqual(1, observed);
            resolver.Dispose();
        }

        [Test]
        public void Build_WithProjectInstaller_TheTelemetryWriterAndRunStoreAreOneJsonlStore()
        {
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            ITelemetryWriter writer = resolver.Resolve<ITelemetryWriter>();
            ITelemetryRunStore store = resolver.Resolve<ITelemetryRunStore>();

            Assert.IsInstanceOf<JsonlTelemetryStore>(writer);
            Assert.AreSame(writer, store);
            resolver.Dispose();
        }

        [Test]
        public void Build_WithProjectInstaller_TheRecorderWaitsForBootToInitialise()
        {
            // Resolution only: the scope's Awake, not the container, initialises the recorder.
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            TelemetryRecorder recorder = resolver.Resolve<TelemetryRecorder>();

            Assert.AreSame(recorder, resolver.Resolve<TelemetryRecorder>());
            Assert.IsFalse(recorder.Initialized);
            Assert.IsNotNull(resolver.Resolve<TelemetryRunService>());
            Assert.IsNotNull(resolver.Resolve<LaunchService>());
            resolver.Dispose();
        }

        [Test]
        public void Build_WithProjectInstaller_LaunchServiceIsOneSingleton()
        {
            // Resolution only: a launch here would write to the real persistent-data folder.
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            LaunchService launch = resolver.Resolve<LaunchService>();

            Assert.IsNotNull(launch);
            Assert.AreSame(launch, resolver.Resolve<LaunchService>());
            Assert.AreEqual(SessionStage.NoSession, resolver.Resolve<ISessionQueries>().Stage);
            resolver.Dispose();
        }

        /// <summary>
        /// Loads the shipped catalog through the project's own loader and marks the holder loaded, as
        /// the boot sequence does.
        /// </summary>
        /// <param name="resolver">The project container.</param>
        /// <returns>The loaded catalog.</returns>
        private static ContentCatalog LoadShippedCatalog(IObjectResolver resolver)
        {
            ContentCatalog catalog = resolver.Resolve<CatalogLoader>().LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            resolver.Resolve<CatalogHolder>().MarkLoaded(catalog);
            return catalog;
        }
    }
}
