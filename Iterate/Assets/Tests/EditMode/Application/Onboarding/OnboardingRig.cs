using System;
using System.Collections.Generic;
using System.Threading;
using Iterate.Application.Content;
using Iterate.Application.Logging;
using Iterate.Application.Logging.Tests;
using Iterate.Application.Onboarding;
using Iterate.Application.Persistence;
using Iterate.Application.SessionFlow;
using Iterate.Application.SessionFlow.Tests;
using Iterate.Domain.Content;
using Iterate.Domain.Values;
using SessionSurface = Iterate.Application.SessionFlow.SessionFlow;

namespace Iterate.Application.Onboarding.Tests
{
    /// <summary>
    /// One launch of the game, headless: a save store, the persistence service, the fixture catalog,
    /// both lifecycles behind the router, Session Flow over the router, its queries, and the launch
    /// service. A relaunch is a new rig over the same store, which is how the suites model quitting
    /// and starting the game again.
    /// </summary>
    public sealed class OnboardingRig
    {
        /// <summary>
        /// The ordered log the scope factory writes to.
        /// </summary>
        public List<string> Log { get; } = new();

        /// <summary>
        /// The save store this launch reads and writes.
        /// </summary>
        public ISaveFileStore Store { get; }

        /// <summary>
        /// The log sink, for asserting on Warnings and Errors.
        /// </summary>
        public FakeLogSink Sink { get; } = new();

        /// <summary>
        /// The persistence service over the store.
        /// </summary>
        public PersistenceService Persistence { get; }

        /// <summary>
        /// The loaded fixture catalog.
        /// </summary>
        public ContentCatalog Catalog { get; }

        /// <summary>
        /// The onboarding lifecycle.
        /// </summary>
        public OnboardingLifecycle Onboarding { get; }

        /// <summary>
        /// The router Session Flow is given.
        /// </summary>
        public SessionLifecycleRouter Router { get; }

        /// <summary>
        /// Both scope factories, keeping the live holders.
        /// </summary>
        public RecordingScopeFactory Scopes { get; }

        /// <summary>
        /// The Session surface.
        /// </summary>
        public SessionSurface Flow { get; }

        /// <summary>
        /// The queries over the same Session.
        /// </summary>
        public SessionQueryService Queries { get; }

        /// <summary>
        /// The ordinary identity source.
        /// </summary>
        public FixedSessionIdentitySource Identities { get; } = new();

        /// <summary>
        /// The launch service.
        /// </summary>
        public LaunchService Launch { get; }

        /// <summary>
        /// Builds one launch over a store.
        /// </summary>
        /// <param name="store">The store; a new empty fake when null.</param>
        /// <param name="firstSystem">The System to put first in the catalog, so launches play it; the main System when null.</param>
        public OnboardingRig(ISaveFileStore store = null, SystemID? firstSystem = null)
        {
            Store = store ?? new Iterate.Application.Persistence.Tests.FakeSaveFileStore();
            GameLoggerFactory loggers = new(Sink, new LogFilterConfig(LogLevel.Info));
            Persistence = new PersistenceService(Store, loggers);
            CatalogHolder holder = new();
            Catalog = firstSystem.HasValue ? PlayFirst(SessionFlowFixtures.Catalog(), firstSystem.Value) : SessionFlowFixtures.Catalog();
            holder.MarkLoaded(Catalog);
            Onboarding = new OnboardingLifecycle(Persistence, holder, loggers);
            Router = new SessionLifecycleRouter(Onboarding, new CanonicalSessionLifecycle());
            Scopes = new RecordingScopeFactory(Log);
            Flow = new SessionSurface(
                holder,
                Router,
                Scopes,
                Scopes,
                Array.Empty<ICommandGate>(),
                Array.Empty<ICommandObserver>()
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
        }

        /// <summary>
        /// Rebuilds a catalog with one System moved first and the play archetype first, which is what a
        /// launch plays.
        /// </summary>
        /// <param name="catalog">The fixture catalog.</param>
        /// <param name="first">The System to play.</param>
        /// <returns>The reordered catalog.</returns>
        internal static ContentCatalog PlayFirst(ContentCatalog catalog, SystemID first)
        {
            List<SystemDefinition> systems = new();
            foreach (SystemDefinition system in catalog.Systems)
            {
                if (system.ID == first)
                    systems.Insert(0, system);
                else
                    systems.Add(system);
            }

            List<StarterArchetypeDefinition> archetypes = new() { SessionFlowPlayContent.Archetype() };
            foreach (StarterArchetypeDefinition archetype in catalog.StarterArchetypes)
            {
                if (archetype.ID != archetypes[0].ID)
                    archetypes.Add(archetype);
            }

            return new ContentCatalog(
                catalog.Revision,
                catalog.Parameters,
                catalog.Instructions,
                catalog.Structures,
                catalog.Directives,
                catalog.Dependencies,
                catalog.Patches,
                catalog.Utilities,
                catalog.ProcessRules,
                catalog.Cores,
                catalog.ProcessConfigurations,
                catalog.Shops,
                catalog.Pools,
                catalog.RewardPackages,
                catalog.Routes,
                archetypes,
                systems
            );
        }

        /// <summary>
        /// Waits for every queued save and reads the checkpoint back through the persistence service.
        /// </summary>
        /// <returns>The saved checkpoint, or null when there is none.</returns>
        public CheckpointState SavedCheckpoint()
        {
            Onboarding.PendingWork.GetAwaiter().GetResult();
            LoadResult<CheckpointState> loaded = Persistence.LoadCheckpointAsync(CancellationToken.None).GetAwaiter().GetResult();
            return loaded.Outcome == LoadOutcome.Loaded ? loaded.Value : null;
        }

        /// <summary>
        /// Waits for every queued save and reads the meta back.
        /// </summary>
        /// <returns>The saved meta, or the default when absent.</returns>
        public MetaState SavedMeta()
        {
            Onboarding.PendingWork.GetAwaiter().GetResult();
            return Persistence.LoadMetaAsync(CancellationToken.None).GetAwaiter().GetResult().ValueOr(MetaState.Default);
        }

        /// <summary>
        /// Begins through the launch service and waits for it.
        /// </summary>
        /// <returns>The begin result.</returns>
        public SessionBeginResult Begin()
        {
            return Launch.BeginAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Counts the log entries at a level.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <returns>The count.</returns>
        public int CountAt(LogLevel level)
        {
            int count = 0;
            foreach (LogEntry entry in Sink.Entries)
            {
                if (entry.Level == level)
                    count++;
            }

            return count;
        }
    }
}
