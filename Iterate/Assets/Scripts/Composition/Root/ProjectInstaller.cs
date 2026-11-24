using System.IO;
using Didionysymus.Lattice.Runtime;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Application.Logging;
using Iterate.Application.Onboarding;
using Iterate.Application.Persistence;
using Iterate.Application.SessionFlow;
using Iterate.Application.Telemetry;
using Iterate.Infrastructure.Content;
using Iterate.Infrastructure.Identity;
using Iterate.Infrastructure.Logging;
using Iterate.Infrastructure.Persistence;
using Iterate.Infrastructure.Telemetry;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// Registers the Session-independent services owned by the project scope: the logging seam, the
    /// catalog pipeline (reader, validator, freezer, holder, loader, the StreamingAssets directory
    /// source, and the boot-time async-startable adapter), and the persistence seam (the save-file
    /// adapter over the platform's persistent-data path, and the service above it), and the Session
    /// surface (one Session Flow under its command and event ports, the queries over the same Session,
    /// the lifecycle router over the onboarding and canonical lifecycles, and the two scope factories),
    /// and the launch service with its identity source, and telemetry: the command tap as
    /// Session Flow's one observer, the lifecycle tap over the router as Session Flow's lifecycle, the
    /// command guard over Session Flow as the command port, the JSONL run store in the Editor and
    /// Development builds and the null writer and store otherwise, the clock, run identities and
    /// build versions, the recorder and the run service. Sessions are begun through the launch
    /// service, never through Session Flow directly, because the launch service selects the
    /// lifecycle; commands are issued through the command port, so the guard sees them. No command
    /// gate is registered, so Session Flow receives an empty gate list.
    /// </summary>
    public sealed class ProjectInstaller : IInstaller
    {
        /// <summary>
        /// Registers the project-scope services on the builder.
        /// </summary>
        /// <param name="builder">The container builder to register into.</param>
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(new LogFilterConfig(LogLevel.Info));
            builder.Register<ILogSink, ConsoleLogSink>(Lifetime.Singleton);
            builder.Register<IGameLoggerFactory, GameLoggerFactory>(Lifetime.Singleton);

            builder.Register<CatalogJsonReader>(Lifetime.Singleton);
            builder.Register<ICatalogValidator, CatalogValidator>(Lifetime.Singleton);
            builder.Register<CatalogFreezer>(Lifetime.Singleton);
            builder.Register<CatalogHolder>(Lifetime.Singleton);
            builder.Register<CatalogLoader>(Lifetime.Singleton);
            builder.RegisterFactory<ICatalogFileSource>(
                static resolver => new CatalogDirectorySource(Path.Combine(UnityEngine.Application.streamingAssetsPath, "Catalog")),
                Lifetime.Singleton
            );
            builder.Register<IAsyncStartable, CatalogAsyncStartable>(Lifetime.Singleton);
            
            builder.RegisterFactory<ISaveFileStore>(
                static resolver => new FileSaveStore(UnityEngine.Application.persistentDataPath),
                Lifetime.Singleton
            );
            builder.Register<PersistenceService>(Lifetime.Singleton);

            builder.Register<SessionFlow>(Lifetime.Singleton);
            builder.Register<TelemetryCommandTap>(Lifetime.Singleton);
            builder.RegisterFactory<ICommandObserver>(static resolver => resolver.Resolve<TelemetryCommandTap>(), Lifetime.Singleton);
            builder.RegisterFactory<ISessionCommands>(static resolver => resolver.Resolve<TelemetryCommandGuard>(), Lifetime.Singleton);
            builder.RegisterFactory<ISessionEvents>(static resolver => resolver.Resolve<SessionFlow>(), Lifetime.Singleton);
            builder.Register<ISessionQueries, SessionQueryService>(Lifetime.Singleton);
            builder.Register<CanonicalSessionLifecycle>(Lifetime.Singleton);
            builder.Register<OnboardingLifecycle>(Lifetime.Singleton);
            builder.Register<SessionLifecycleRouter>(Lifetime.Singleton);
            builder.Register<TelemetryLifecycleTap>(Lifetime.Singleton);
            builder.RegisterFactory<ISessionLifecycle>(static resolver => resolver.Resolve<TelemetryLifecycleTap>(), Lifetime.Singleton);
            builder.Register<ISessionIdentitySource, GuidSessionIdentitySource>(Lifetime.Singleton);
            builder.Register<LaunchService>(Lifetime.Singleton);
            builder.RegisterFactory<ISessionScopeFactory>(static resolver => new LatticeSessionScopeFactory(resolver), Lifetime.Singleton);
            builder.Register<IProcessScopeFactory, LatticeProcessScopeFactory>(Lifetime.Singleton);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterFactory<JsonlTelemetryStore>(
                static resolver => new JsonlTelemetryStore(Path.Combine(UnityEngine.Application.persistentDataPath, "telemetry"), resolver.Resolve<IGameLoggerFactory>()),
                Lifetime.Singleton
            );
            builder.RegisterFactory<ITelemetryWriter>(static resolver => resolver.Resolve<JsonlTelemetryStore>(), Lifetime.Singleton);
            builder.RegisterFactory<ITelemetryRunStore>(static resolver => resolver.Resolve<JsonlTelemetryStore>(), Lifetime.Singleton);
#else
            builder.Register<ITelemetryWriter, NullTelemetryWriter>(Lifetime.Singleton);
            builder.Register<ITelemetryRunStore, NullTelemetryRunStore>(Lifetime.Singleton);
#endif
            builder.Register<ITelemetryClock, SystemTelemetryClock>(Lifetime.Singleton);
            builder.Register<ITelemetryRunIdentitySource, GuidTelemetryRunIdentitySource>(Lifetime.Singleton);
            builder.RegisterInstance(new TelemetryBuildInfo(UnityEngine.Application.version, "0.1"));
            builder.Register<TelemetryRecorder>(Lifetime.Singleton);
            builder.RegisterFactory<TelemetryCommandGuard>(static resolver => new TelemetryCommandGuard(resolver.Resolve<SessionFlow>(), resolver.Resolve<TelemetryRecorder>()), Lifetime.Singleton);
            builder.Register<TelemetryRunService>(Lifetime.Singleton);
        }
    }
}