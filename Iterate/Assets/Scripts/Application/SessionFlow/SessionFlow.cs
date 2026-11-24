using System;
using System.Collections.Generic;
using Iterate.Application.Content;
using Iterate.Application.Persistence;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The Session's command surface and the only writer of its state. A command applies only when a
    /// route exists for it in the current stage and every gate permits it; a refused command touches
    /// nothing. Events are raised once the Session is consistent, stage change before state change,
    /// and observers see every command, accepted or refused, after the events. Neither a command nor
    /// a Session begin may be issued from inside another: a re-entrant call throws, because events
    /// precede observers and a nested command would be observed before the one that caused it.
    /// </summary>
    public sealed class SessionFlow : ISessionCommands, ISessionEvents
    {
        private readonly CatalogHolder _catalog;
        private readonly IReadOnlyList<ICommandGate> _gates;
        private readonly IReadOnlyList<ICommandObserver> _observers;
        private readonly StageTransitions _transitions;
        private readonly CommandRegistry _routes;
        private readonly SessionContext _context = new();
        private bool _busy;

        /// <summary>
        /// The context this surface writes, shared with the queries that read it.
        /// </summary>
        internal SessionContext Context => _context;

        /// <summary>
        /// Raised when the stage moves.
        /// </summary>
        public event Action<StageChange> StageChanged;

        /// <summary>
        /// Raised after an accepted command, naming the areas it changed.
        /// </summary>
        public event Action<StateChange> StateChanged;

        /// <summary>
        /// Creates the Session surface. No Session is active until one begins.
        /// </summary>
        /// <param name="catalog">The holder the catalog loads into.</param>
        /// <param name="lifecycle">The lifecycle told of each moment.</param>
        /// <param name="sessionScopes">The Session scope factory.</param>
        /// <param name="processScopes">The Process scope factory.</param>
        /// <param name="gates">The command gates, consulted in order.</param>
        /// <param name="observers">The command observers, notified in order.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public SessionFlow(
            CatalogHolder catalog,
            ISessionLifecycle lifecycle,
            ISessionScopeFactory sessionScopes,
            IProcessScopeFactory processScopes,
            IReadOnlyList<ICommandGate> gates,
            IReadOnlyList<ICommandObserver> observers
        )
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _gates = gates ?? throw new ArgumentNullException(nameof(gates));
            _observers = observers ?? throw new ArgumentNullException(nameof(observers));
            _transitions = new StageTransitions(lifecycle, sessionScopes, processScopes);
            _routes = SessionCommandRoutes.Build(
                new BuildCommandHandler(),
                new BranchCommandHandler(_transitions),
                new ShopCommandHandler(_transitions),
                new RewardCommandHandler(_transitions),
                new FlowCommandHandler(_transitions)
            );
        }

        /// <summary>
        /// Begins a fresh Session over a Starter Archetype and enters the System's first step.
        /// </summary>
        /// <param name="archetype">The Starter Archetype to seed from.</param>
        /// <param name="system">The System to play.</param>
        /// <param name="sessionIdentity">The Session's identity.</param>
        /// <param name="sessionSeedIdentity">The Session's seed identity.</param>
        /// <returns>The begin result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the archetype is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when called from inside a command or a begin.</exception>
        public SessionBeginResult BeginSession(
            StarterArchetypeDefinition archetype,
            SystemID system,
            string sessionIdentity,
            string sessionSeedIdentity
        )
        {
            if (archetype == null)
                throw new ArgumentNullException(nameof(archetype));

            EnterGuard();
            try
            {
                SessionBeginRejection refused = CheckBegin(system, out ContentCatalog catalog, out SystemDefinition definition);
                if (refused != SessionBeginRejection.None)
                    return new SessionBeginResult(false, refused);

                SessionState session = SessionState.Create(
                    catalog,
                    archetype,
                    system.Value,
                    sessionIdentity,
                    sessionSeedIdentity
                );

                SessionStage previous = _context.Stage;
                SessionEndReason previousReason = _context.EndReason;
                _transitions.Begin(_context, catalog, new StagePlan(definition, catalog), session);
                RequireEntered(_transitions.EnterStep(_context), previous, previousReason);
                RaiseStageChanged(previous);
                return new SessionBeginResult(true, SessionBeginRejection.None);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// Begins a restored Session at a checkpoint destination: the plan is positioned on the
        /// destination's Process, its route committed, and the Process re-entered by the destination's
        /// recovery.
        /// </summary>
        /// <param name="restored">The restored Session.</param>
        /// <param name="system">The System the Session plays.</param>
        /// <param name="destination">Where the Session resumes.</param>
        /// <returns>The begin result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the Session or the destination is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when called from inside a command or a begin.</exception>
        public SessionBeginResult BeginSession(SessionState restored, SystemID system, CheckpointDestination destination)
        {
            if (restored == null)
                throw new ArgumentNullException(nameof(restored));

            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            EnterGuard();
            try
            {
                SessionBeginRejection refused = CheckBegin(system, out ContentCatalog catalog, out SystemDefinition definition);
                if (refused != SessionBeginRejection.None)
                    return new SessionBeginResult(false, refused);

                StagePlan plan = new(definition, catalog);
                if (destination.Route.HasValue && !TryCommitOnPlan(plan, destination.Route.Value))
                    return new SessionBeginResult(false, SessionBeginRejection.UnknownRoute);

                if (!plan.TryRewindTo(destination.Process)
                    || !catalog.TryGetProcessConfiguration(destination.Process, out ProcessConfigurationDefinition configuration))
                {
                    return new SessionBeginResult(false, SessionBeginRejection.UnknownProcess);
                }

                SessionStage previous = _context.Stage;
                SessionEndReason previousReason = _context.EndReason;
                _transitions.Begin(_context, catalog, plan, restored);
                if (destination.Recovery == CheckpointRecovery.BranchConfiguration && configuration.ActiveBranch != null)
                {
                    _transitions.EnterBranchConfiguration(_context, configuration);
                }
                else
                {
                    ProcessCreationResult created = _transitions.EnterProcess(_context, configuration);
                    RequireEntered(new HandlerOutcome(created.Succeeded, created, ChangedAreas.None), previous, previousReason);
                }

                RaiseStageChanged(previous);
                return new SessionBeginResult(true, SessionBeginRejection.None);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// Applies a command if a route exists for it in the current stage and every gate permits it.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the command is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when called from inside a command or a begin.</exception>
        public CommandResult Execute(ISessionCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            EnterGuard();
            try
            {
                CommandResult result = Apply(command);
                for (int index = 0; index < _observers.Count; index++)
                    _observers[index].Observed(command, result);

                return result;
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// Runs a command through legality, the gates and its handler, raising the events an accepted
        /// command earns.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The result.</returns>
        private CommandResult Apply(ISessionCommand command)
        {
            SessionStage stage = _context.Stage;
            if (!_context.IsActive)
                return CommandResult.Reject(CommandRejection.NoSession, null, stage);

            if (!_routes.TryGet(command.GetType(), stage, out CommandRoute route))
                return CommandResult.Reject(CommandRejection.WrongStage, null, stage);

            for (int index = 0; index < _gates.Count; index++)
            {
                if (!_gates[index].Permit(command, stage))
                    return CommandResult.Reject(CommandRejection.Gated, null, stage);
            }

            HandlerOutcome outcome = route.Apply(_context, command);
            if (!outcome.Accepted)
                return CommandResult.Reject(CommandRejection.DomainRejected, outcome.DomainResult, _context.Stage);

            RaiseStageChanged(stage);
            if (outcome.Areas != ChangedAreas.None)
                StateChanged?.Invoke(new StateChange(outcome.Areas));

            return CommandResult.Accept(outcome.DomainResult, _context.Stage);
        }

        /// <summary>
        /// Checks what both begins share: no Session in progress, a loaded catalog, and a known System.
        /// </summary>
        /// <param name="system">The System.</param>
        /// <param name="catalog">The loaded catalog, or null.</param>
        /// <param name="definition">The System's definition, or null.</param>
        /// <returns>The rejection, or None.</returns>
        private SessionBeginRejection CheckBegin(SystemID system, out ContentCatalog catalog, out SystemDefinition definition)
        {
            catalog = null;
            definition = null;
            if (_context.IsActive)
                return SessionBeginRejection.SessionAlreadyActive;

            if (!_catalog.TryGetCatalog(out catalog))
                return SessionBeginRejection.CatalogUnavailable;

            if (!catalog.TryGetSystem(system, out definition))
                return SessionBeginRejection.UnknownSystem;

            return SessionBeginRejection.None;
        }

        /// <summary>
        /// Undoes a begin whose first entry was refused, which only a content defect can cause, and
        /// throws.
        /// </summary>
        /// <param name="entered">The entry's outcome.</param>
        /// <param name="previous">The stage before the begin.</param>
        /// <param name="previousReason">The end reason before the begin.</param>
        /// <exception cref="InvalidOperationException">Thrown when the entry was refused.</exception>
        private void RequireEntered(HandlerOutcome entered, SessionStage previous, SessionEndReason previousReason)
        {
            if (entered.Accepted)
                return;

            _transitions.AbortBegin(_context, previous, previousReason);
            throw new InvalidOperationException($"The Session could not enter its first step: {entered.DomainResult}.");
        }

        /// <summary>
        /// Raises the stage change when the stage moved.
        /// </summary>
        /// <param name="previous">The stage before.</param>
        private void RaiseStageChanged(SessionStage previous)
        {
            if (_context.Stage != previous)
                StageChanged?.Invoke(new StageChange(previous, _context.Stage, _context.EndReason));
        }

        /// <summary>
        /// Refuses a re-entrant call and marks the surface busy.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a command or a begin is in progress.</exception>
        private void EnterGuard()
        {
            if (_busy)
                throw new InvalidOperationException("A Session command or begin was issued from inside another; defer it until the current one returns.");

            _busy = true;
        }

        /// <summary>
        /// Advances a fresh plan to the route choice offering a route and commits to it.
        /// </summary>
        /// <param name="plan">The plan.</param>
        /// <param name="route">The route.</param>
        /// <returns>True when the route was committed.</returns>
        private static bool TryCommitOnPlan(StagePlan plan, RouteID route)
        {
            while (plan.Current.Kind != PlannedStepKind.End)
            {
                if (plan.Current.Kind == PlannedStepKind.RouteSelection && plan.TryCommitRoute(route))
                    return true;

                plan.Advance();
            }

            return false;
        }
    }
}