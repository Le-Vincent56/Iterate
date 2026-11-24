using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Moves a Session between stages: begins and ends it, enters the plan's steps, configures the
    /// Branch, and enters a Process in its contracted order. It writes only the context it is handed
    /// and keeps no state of its own. Content the validated catalog guarantees, such as a stage's
    /// shop or Process configuration, is a defect when missing and throws.
    /// </summary>
    internal sealed class StageTransitions
    {
        private const ChangedAreas ProcessAreas = ChangedAreas.Build | ChangedAreas.Buffer | ChangedAreas.Bytes | ChangedAreas.Counters;
        private readonly ISessionLifecycle _lifecycle;
        private readonly ISessionScopeFactory _sessionScopes;
        private readonly IProcessScopeFactory _processScopes;

        /// <summary>
        /// Creates the transitions over the lifecycle and the two scope factories.
        /// </summary>
        /// <param name="lifecycle">The lifecycle told of each moment.</param>
        /// <param name="sessionScopes">The Session scope factory.</param>
        /// <param name="processScopes">The Process scope factory.</param>
        internal StageTransitions(ISessionLifecycle lifecycle, ISessionScopeFactory sessionScopes, IProcessScopeFactory processScopes)
        {
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _sessionScopes = sessionScopes ?? throw new ArgumentNullException(nameof(sessionScopes));
            _processScopes = processScopes ?? throw new ArgumentNullException(nameof(processScopes));
        }

        /// <summary>
        /// Starts a Session on a context: takes the catalog, opens the Session scope and installs the
        /// plan. The stage is left for the caller to enter.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="catalog">The catalog for the Session.</param>
        /// <param name="plan">The positioned plan.</param>
        /// <param name="session">The Session.</param>
        internal void Begin(
            SessionContext context,
            ContentCatalog catalog,
            StagePlan plan,
            SessionState session
        )
        {
            context.ClearHolders();
            context.Catalog = catalog;
            context.Plan = plan;
            context.Session = session;
            context.EndReason = SessionEndReason.None;
            context.SessionScope = _sessionScopes.Open(session);
        }

        /// <summary>
        /// Enters the plan's current step.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The entry's outcome; refused only when the entered Process could not be created.</returns>
        internal HandlerOutcome EnterStep(SessionContext context)
        {
            PlannedStep step = context.Plan.Current;
            switch (step.Kind)
            {
                case PlannedStepKind.Process:
                    return EnterProcessStep(context, ConfigurationOf(context, step.Process.Value));

                case PlannedStepKind.Shop:
                    return OpenShop(context, step.Shop.Value);

                case PlannedStepKind.RouteSelection:
                    context.Stage = SessionStage.RouteSelection;
                    return new HandlerOutcome(true, null, ChangedAreas.None);

                default:
                    Complete(context);
                    return new HandlerOutcome(true, null, ChangedAreas.None);
            }
        }

        /// <summary>
        /// Enters a Process step: through Branch configuration when the Process declares a Branch,
        /// otherwise straight into the Process.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="configuration">The Process configuration.</param>
        /// <returns>The entry's outcome.</returns>
        internal HandlerOutcome EnterProcessStep(SessionContext context, ProcessConfigurationDefinition configuration)
        {
            if (configuration.ActiveBranch != null)
            {
                EnterBranchConfiguration(context, configuration);
                return new HandlerOutcome(true, null, ChangedAreas.Branch);
            }

            ProcessCreationResult created = EnterProcess(context, configuration);
            return new HandlerOutcome(created.Succeeded, created, created.Succeeded ? ProcessAreas : ChangedAreas.None);
        }

        /// <summary>
        /// Opens Branch configuration against the Process's authored constraints. The draft is seeded
        /// from the confirmed Branch when one exists, otherwise from the Repository.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="configuration">The Process configuration.</param>
        internal void EnterBranchConfiguration(SessionContext context, ProcessConfigurationDefinition configuration)
        {
            ActiveBranchSpec spec = configuration.ActiveBranch;
            BranchConstraints constraints = new(
                spec.Capacity,
                spec.Required,
                spec.Quarantined,
                spec.RecommendedTags,
                spec.CautionTags
            );

            SessionState session = context.Session;
            session.ActiveBranch.BeginConfiguration(constraints, session.Repository.Snapshot());
            context.Stage = SessionStage.BranchConfiguration;
        }

        /// <summary>
        /// Enters a Process in the contracted order: the lifecycle is told before anything is
        /// allocated, setup resolves, the Process is created, and only a created Process gets its Build
        /// and scope. A refused creation changes nothing further and leaves the stage.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="configuration">The Process configuration.</param>
        /// <returns>The creation result.</returns>
        internal ProcessCreationResult EnterProcess(SessionContext context, ProcessConfigurationDefinition configuration)
        {
            SessionState session = context.Session;
            ContentCatalog catalog = context.Catalog;

            _lifecycle.OnProcessEntry(session, configuration.ID, context.Plan.CommittedRoute);

            ProcessSetup setup = ProcessSetupResolver.Resolve(
                configuration,
                catalog.Parameters,
                session.Economy.SetupEffectsFor(configuration.ID)
            );

            ProcessCreationResult created = ProcessState.Create(
                configuration,
                setup,
                catalog,
                session,
                session.ActiveBranch.Confirmed
            );

            if (!created.Succeeded)
                return created;

            BuildState build = new(created.State.InitialArrangement, created.State.Buffer, catalog.Parameters);
            context.Process = created.State;
            context.Build = build;
            context.ProcessScope = _processScopes.Open(context.SessionScope, created.State, build);
            context.Stage = SessionStage.Build;
            return created;
        }

        /// <summary>
        /// Completes the Session: the lifecycle is told, the Session scope closes, and the Session ends
        /// as completed.
        /// </summary>
        /// <param name="context">The context.</param>
        internal void Complete(SessionContext context)
        {
            _lifecycle.OnSessionCompleted(context.Session);
            End(context, SessionEndReason.Completed);
        }

        /// <summary>
        /// Abandons the Session: the lifecycle is told, the Process scope closes before the Session
        /// scope, and the Session ends as abandoned.
        /// </summary>
        /// <param name="context">The context.</param>
        internal void Abandon(SessionContext context)
        {
            _lifecycle.OnSessionAbandoned(context.Session);
            End(context, SessionEndReason.Abandoned);
        }

        /// <summary>
        /// Moves past the current step and enters the next one. A next Process that cannot be created
        /// is a content defect, because the step was reached without any player choice.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The areas the entry changed.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the next Process cannot be created.</exception>
        internal ChangedAreas AdvanceToNextStep(SessionContext context)
        {
            context.Plan.Advance();
            HandlerOutcome entered = EnterStep(context);
            if (!entered.Accepted)
                throw new InvalidOperationException($"The Session could not enter its next step: {entered.DomainResult}.");

            return entered.Areas;
        }

        /// <summary>
        /// Ends the rewards stage when every reward is resolved: the Process's Utilities expire, its
        /// scope closes, and the Session moves to the next step. Unresolved rewards keep the stage.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The areas the step change touched; none while rewards remain.</returns>
        internal ChangedAreas FinishRewardsIfResolved(SessionContext context)
        {
            if (!context.Rewards.IsComplete)
                return ChangedAreas.None;

            context.Session.Economy.ExpireUtilitiesFor(context.Process.Configuration.ID);
            CloseProcess(context);
            return ChangedAreas.Economy | ChangedAreas.Rewards | AdvanceToNextStep(context);
        }

        /// <summary>
        /// Fails the current Process and follows the lifecycle's disposition: end the Session, restart
        /// the Process from its initial scripted state, or return to Branch configuration seeded from
        /// the confirmed Branch.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The areas the failure changed.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the Process cannot be re-entered.</exception>
        internal ChangedAreas Fail(SessionContext context)
        {
            ProcessConfigurationDefinition configuration = context.Process.Configuration;
            ProcessFailureDisposition disposition = _lifecycle.OnProcessFailed(context.Session, configuration.ID, context.Plan.CommittedRoute);
            if (disposition == ProcessFailureDisposition.EndSession)
            {
                End(context, SessionEndReason.Failed);
                return ChangedAreas.None;
            }

            CloseProcess(context);
            HandlerOutcome entered = disposition == ProcessFailureDisposition.RestartProcess
                ? ReenterProcess(context, configuration)
                : EnterProcessStep(context, configuration);

            if (!entered.Accepted)
                throw new InvalidOperationException($"The failed Process could not be re-entered: {entered.DomainResult}.");

            return entered.Areas | ChangedAreas.Execution;
        }

        /// <summary>
        /// Re-enters a Process directly, without Branch configuration.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="configuration">The Process configuration.</param>
        /// <returns>The entry's outcome.</returns>
        private HandlerOutcome ReenterProcess(SessionContext context, ProcessConfigurationDefinition configuration)
        {
            ProcessCreationResult created = EnterProcess(context, configuration);
            return new HandlerOutcome(created.Succeeded, created, created.Succeeded ? ProcessAreas : ChangedAreas.None);
        }

        /// <summary>
        /// Closes the Process scope and drops every Process holder.
        /// </summary>
        /// <param name="context">The context.</param>
        private static void CloseProcess(SessionContext context)
        {
            context.ProcessScope?.Dispose();
            context.ProcessScope = null;
            context.Process = null;
            context.Build = null;
            context.Result = null;
            context.Rewards = null;
        }

        /// <summary>
        /// Undoes a Session begin that could not enter its first step: the Session scope closes and the
        /// context returns to the stage it had before.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="previous">The stage before the begin.</param>
        /// <param name="previousReason">The end reason before the begin.</param>
        internal void AbortBegin(SessionContext context, SessionStage previous, SessionEndReason previousReason)
        {
            CloseScopes(context);
            context.ClearHolders();
            context.Stage = previous;
            context.EndReason = previousReason;
        }

        /// <summary>
        /// Looks up a Process configuration the plan names.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="process">The Process.</param>
        /// <returns>The configuration.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the catalog does not define it.</exception>
        internal static ProcessConfigurationDefinition ConfigurationOf(SessionContext context, ProcessID process)
        {
            if (!context.Catalog.TryGetProcessConfiguration(process, out ProcessConfigurationDefinition configuration))
                throw new InvalidOperationException($"The catalog defines no configuration for {process.Value}, which the System names.");

            return configuration;
        }

        /// <summary>
        /// Opens a shop step targeting the Process the plan leads into.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="shop">The shop.</param>
        /// <returns>The accepted outcome carrying the open result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the shop cannot be opened.</exception>
        private static HandlerOutcome OpenShop(SessionContext context, ShopID shop)
        {
            if (!context.Catalog.TryGetShop(shop, out ShopDefinition definition))
                throw new InvalidOperationException($"The catalog does not define {shop.Value}, which the System names.");

            ProcessID? target = context.Plan.NextProcessAfterCurrent();
            if (!target.HasValue)
                throw new InvalidOperationException($"The shop {shop.Value} precedes no Process the plan can name.");

            ShopOpenResult opened = ShopState.Open(definition, context.Catalog, context.Session, target.Value);
            if (!opened.Succeeded)
                throw new InvalidOperationException($"The shop {shop.Value} could not open: {opened.Rejection}.");

            context.Shop = opened.Shop;
            context.Stage = SessionStage.Shop;
            return new HandlerOutcome(true, opened, ChangedAreas.Shop);
        }

        /// <summary>
        /// Ends the Session: scopes close, holders drop, and the stage records why.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="reason">Why the Session ended.</param>
        private static void End(SessionContext context, SessionEndReason reason)
        {
            CloseScopes(context);
            context.ClearHolders();
            context.Stage = SessionStage.SessionEnded;
            context.EndReason = reason;
        }

        /// <summary>
        /// Closes the Process scope, if any, and then the Session scope.
        /// </summary>
        /// <param name="context">The context.</param>
        private static void CloseScopes(SessionContext context)
        {
            context.ProcessScope?.Dispose();
            context.ProcessScope = null;
            context.SessionScope?.Dispose();
            context.SessionScope = null;
        }
    }
}