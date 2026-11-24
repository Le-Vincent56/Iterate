using System;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Content;
using Iterate.Application.Persistence;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Application.Onboarding
{
    /// <summary>
    /// Decides what a launch begins and begins it. Until onboarding is complete, a launch enters
    /// onboarding: at its saved checkpoint when one restores, otherwise from the start, clearing any
    /// checkpoint that cannot be used. Once onboarding is complete, a launch begins an ordinary
    /// Session of the System with fresh identities. This is the way a Session is begun; the lifecycle
    /// mode is set here, so a Session begun around it runs as an ordinary one.
    ///
    /// Its awaits keep the caller's context, because beginning a Session raises Session Flow's events
    /// on the thread presenters listen on.
    /// </summary>
    public sealed class LaunchService
    {
        private const string OnboardingSessionIdentity = "onboarding";
        private const string OnboardingSeedIdentity = "onboarding-seed";
        private readonly PersistenceService _persistence;
        private readonly CatalogHolder _catalog;
        private readonly SessionFlow.SessionFlow _flow;
        private readonly ISessionQueries _queries;
        private readonly SessionLifecycleRouter _router;
        private readonly OnboardingLifecycle _onboarding;
        private readonly ISessionIdentitySource _identities;

        /// <summary>
        /// The begin being issued, only while it is inside Session Flow's begin: its mode, and
        /// whether it resumes at a checkpoint. A refused restore's fresh fallback is described as
        /// fresh. Null at every other time, so a Session begun around this service has none.
        /// Telemetry reads it to label a run.
        /// </summary>
        public LaunchDecision Beginning { get; private set; }

        /// <summary>
        /// Creates the launch service.
        /// </summary>
        /// <param name="persistence">The persistence service.</param>
        /// <param name="catalog">The holder the catalog loads into.</param>
        /// <param name="flow">The Session surface.</param>
        /// <param name="queries">The queries over the same Session.</param>
        /// <param name="router">The lifecycle router whose mode is set before each begin.</param>
        /// <param name="onboarding">The onboarding lifecycle.</param>
        /// <param name="identities">The source of ordinary Session identities.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public LaunchService(
            PersistenceService persistence,
            CatalogHolder catalog,
            SessionFlow.SessionFlow flow,
            ISessionQueries queries,
            SessionLifecycleRouter router,
            OnboardingLifecycle onboarding,
            ISessionIdentitySource identities
        )
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _onboarding = onboarding ?? throw new ArgumentNullException(nameof(onboarding));
            _identities = identities ?? throw new ArgumentNullException(nameof(identities));
        }

        /// <summary>
        /// Decides what the next launch begins, after any save still in flight has finished.
        /// </summary>
        /// <param name="cancellationToken">Cancels the file reads.</param>
        /// <returns>The decision.</returns>
        public async Task<LaunchDecision> DecideAsync(CancellationToken cancellationToken)
        {
            LaunchPlan plan = await PlanAsync(cancellationToken);
            return plan.Decision;
        }

        /// <summary>
        /// Begins the Session the launch decision calls for. Refused, changing nothing, while a Session
        /// is active.
        /// </summary>
        /// <param name="cancellationToken">Cancels the file reads.</param>
        /// <returns>The begin result.</returns>
        public async Task<SessionBeginResult> BeginAsync(CancellationToken cancellationToken)
        {
            if (IsSessionActive())
                return new SessionBeginResult(false, SessionBeginRejection.SessionAlreadyActive);

            if (!_catalog.TryGetCatalog(out ContentCatalog catalog))
                return new SessionBeginResult(false, SessionBeginRejection.CatalogUnavailable);

            LaunchPlan plan = await PlanAsync(cancellationToken);
            SystemDefinition system = catalog.Systems[0];
            StarterArchetypeDefinition archetype = catalog.StarterArchetypes[0];

            if (plan.Decision.Mode == SessionMode.Ordinary)
            {
                _router.Mode = SessionMode.Ordinary;
                SessionIdentities identities = _identities.Next();
                return BeginFresh(
                    new LaunchDecision(SessionMode.Ordinary, false),
                    archetype,
                    system.ID,
                    identities.SessionIdentity,
                    identities.SeedIdentity
                );
            }

            _router.Mode = SessionMode.Onboarding;
            if (plan.Decision.ResumesAtCheckpoint)
            {
                _onboarding.Resume(plan.Checkpoint);
                SessionBeginResult resumed;
                Beginning = new LaunchDecision(SessionMode.Onboarding, true);
                try
                {
                    resumed = _flow.BeginSession(plan.Restored.Session, system.ID, plan.Restored.Destination);
                }
                finally
                {
                    Beginning = null;
                }

                if (resumed.Succeeded)
                    return resumed;
            }

            if (plan.CheckpointPresent)
                await _persistence.ClearCheckpointAsync(cancellationToken);

            _onboarding.Forget();
            return BeginFresh(
                new LaunchDecision(SessionMode.Onboarding, false),
                archetype,
                system.ID,
                OnboardingSessionIdentity,
                OnboardingSeedIdentity
            );
        }

        /// <summary>
        /// Issues a fresh begin, with <see cref="Beginning"/> describing it for its duration.
        /// </summary>
        /// <param name="beginning">The begin being issued.</param>
        /// <param name="archetype">The starter archetype.</param>
        /// <param name="system">The System to play.</param>
        /// <param name="sessionIdentity">The Session identity.</param>
        /// <param name="seedIdentity">The seed identity.</param>
        /// <returns>Session Flow's begin result.</returns>
        private SessionBeginResult BeginFresh(
            LaunchDecision beginning,
            StarterArchetypeDefinition archetype,
            SystemID system,
            string sessionIdentity,
            string seedIdentity
        )
        {
            Beginning = beginning;
            try
            {
                return _flow.BeginSession(archetype, system, sessionIdentity, seedIdentity);
            }
            finally
            {
                Beginning = null;
            }
        }

        /// <summary>
        /// Resets onboarding for a developer: the checkpoint is deleted and the completion flag cleared,
        /// so the next launch is a first launch. Refused, touching no file, while a Session is active.
        /// </summary>
        /// <param name="cancellationToken">Cancels the file work.</param>
        /// <returns>The reset result.</returns>
        public async Task<OnboardingResetResult> ResetOnboardingAsync(CancellationToken cancellationToken)
        {
            if (IsSessionActive())
                return new OnboardingResetResult(true, null);

            await _onboarding.PendingWork;
            LoadResult<MetaState> meta = await _persistence.LoadMetaAsync(cancellationToken);
            ResetOnboardingResult reset = await _persistence.ResetOnboardingAsync(meta.ValueOr(MetaState.Default), cancellationToken);
            _onboarding.Forget();
            return new OnboardingResetResult(false, reset);
        }

        /// <summary>
        /// Reads the save files, after any save still in flight, into a launch plan.
        /// </summary>
        /// <param name="cancellationToken">Cancels the file reads.</param>
        /// <returns>The plan.</returns>
        private async Task<LaunchPlan> PlanAsync(CancellationToken cancellationToken)
        {
            await _onboarding.PendingWork;
            LoadResult<MetaState> meta = await _persistence.LoadMetaAsync(cancellationToken);
            if (meta.ValueOr(MetaState.Default).OnboardingComplete)
                return new LaunchPlan(new LaunchDecision(SessionMode.Ordinary, false), null, null, false);

            LoadResult<CheckpointState> checkpoint = await _persistence.LoadCheckpointAsync(cancellationToken);
            bool present = checkpoint.Outcome != LoadOutcome.Absent;
            if (checkpoint.Outcome != LoadOutcome.Loaded || !_catalog.TryGetCatalog(out ContentCatalog catalog))
                return new LaunchPlan(new LaunchDecision(SessionMode.Onboarding, false), null, null, present);

            CheckpointRestoreResult restored = CheckpointRestore.Restore(checkpoint.Value, catalog);
            if (!restored.Succeeded)
                return new LaunchPlan(new LaunchDecision(SessionMode.Onboarding, false), null, null, present);

            return new LaunchPlan(new LaunchDecision(SessionMode.Onboarding, true), checkpoint.Value, restored, present);
        }

        /// <summary>
        /// Whether a Session is in progress.
        /// </summary>
        /// <returns>True outside the no-Session and ended stages.</returns>
        private bool IsSessionActive()
        {
            return _queries.Stage != SessionStage.NoSession && _queries.Stage != SessionStage.SessionEnded;
        }
    }
}