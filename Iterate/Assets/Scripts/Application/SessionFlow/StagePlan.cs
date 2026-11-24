using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// A cursor over a System's authored stages. Committing a route queues that route's shop and
    /// then its Process ahead of the next authored stage, in that order, because the shop precedes
    /// the Process in play. Past the last stage the plan yields an end step and stays there. The plan
    /// holds no Session state, so it can be positioned before a Session opens.
    /// </summary>
    public sealed class StagePlan
    {
        private static readonly IReadOnlyList<RouteID> _noRoutes = Array.Empty<RouteID>();

        private static readonly PlannedStep _end = new(
            PlannedStepKind.End,
            null,
            null,
            _noRoutes
        );

        private readonly SystemDefinition _system;
        private readonly ContentCatalog _catalog;
        private readonly List<PlannedStep> _pending = new(2);
        private int _cursor;

        /// <summary>
        /// The step the Session stands on.
        /// </summary>
        public PlannedStep Current { get; private set; }

        /// <summary>
        /// The route the Session committed to, or null before any commitment.
        /// </summary>
        public RouteID? CommittedRoute { get; private set; }

        /// <summary>
        /// Creates a plan standing on the System's first authored stage.
        /// </summary>
        /// <param name="system">The System to walk.</param>
        /// <param name="catalog">The catalog the System's routes resolve against.</param>
        /// <exception cref="ArgumentNullException">Thrown when the System or the catalog is null.</exception>
        public StagePlan(SystemDefinition system, ContentCatalog catalog)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _cursor = 0;
            Current = StepAt(0);
        }

        /// <summary>
        /// Moves to the next step: a queued route step first, otherwise the next authored stage, and
        /// the end step past the last one. Advancing from the end step stays on it.
        /// </summary>
        /// <returns>The new current step.</returns>
        public PlannedStep Advance()
        {
            if (Current.Kind == PlannedStepKind.End)
                return Current;

            if (_pending.Count > 0)
            {
                Current = _pending[0];
                _pending.RemoveAt(0);
                return Current;
            }

            _cursor++;
            Current = StepAt(_cursor);
            return Current;
        }

        /// <summary>
        /// Commits to one of the routes the current route choice offers, queueing the route's shop and
        /// then its Process. Refused, with nothing changed, off a route choice, for a route the choice
        /// does not offer, for a route the catalog does not define, and once a route is committed.
        /// </summary>
        /// <param name="route">The route to commit to.</param>
        /// <returns>True when the route was committed.</returns>
        public bool TryCommitRoute(RouteID route)
        {
            if (Current.Kind != PlannedStepKind.RouteSelection || CommittedRoute.HasValue)
                return false;

            if (!Contains(Current.Routes, route))
                return false;

            if (!_catalog.TryGetRoute(route, out RouteDefinition definition))
                return false;

            _pending.Add(new PlannedStep(PlannedStepKind.Shop, null, definition.Shop, _noRoutes));
            _pending.Add(new PlannedStep(PlannedStepKind.Process, definition.Process, null, _noRoutes));
            CommittedRoute = route;
            return true;
        }

        /// <summary>
        /// Repositions the plan on a Process step: an authored Process stage, or the committed route's
        /// Process. Queued route steps are dropped; the committed route is kept.
        /// </summary>
        /// <param name="process">The Process to stand on.</param>
        /// <returns>True when the plan holds a step for the Process; false leaves the plan unchanged.</returns>
        public bool TryRewindTo(ProcessID process)
        {
            IReadOnlyList<SystemStage> stages = _system.Stages;
            for (int index = 0; index < stages.Count; index++)
            {
                if (stages[index].Kind == SystemStageKind.Process && stages[index].Process == process)
                {
                    _pending.Clear();
                    _cursor = index;
                    Current = StepAt(index);
                    return true;
                }
            }

            if (!CommittedRoute.HasValue || !_catalog.TryGetRoute(CommittedRoute.Value, out RouteDefinition route))
                return false;

            if (route.Process != process)
                return false;

            int choice = IndexOfChoiceOffering(CommittedRoute.Value);
            if (choice < 0)
                return false;

            _pending.Clear();
            _cursor = choice;
            Current = new PlannedStep(PlannedStepKind.Process, route.Process, null, _noRoutes);
            return true;
        }

        /// <summary>
        /// The Process the current step leads into: the first Process among the queued route steps,
        /// otherwise the first authored Process after the cursor, stopping at a route choice, whose
        /// Process is not yet known.
        /// </summary>
        /// <returns>The next Process, or null when none is determined yet.</returns>
        public ProcessID? NextProcessAfterCurrent()
        {
            for (int index = 0; index < _pending.Count; index++)
            {
                if (_pending[index].Kind == PlannedStepKind.Process)
                    return _pending[index].Process;
            }

            IReadOnlyList<SystemStage> stages = _system.Stages;
            for (int index = _cursor + 1; index < stages.Count; index++)
            {
                if (stages[index].Kind == SystemStageKind.Process)
                    return stages[index].Process;

                if (stages[index].Kind == SystemStageKind.RouteSelection)
                    return null;
            }

            return null;
        }

        /// <summary>
        /// Builds the step for an authored stage, or the end step past the last one.
        /// </summary>
        /// <param name="index">The authored stage index.</param>
        /// <returns>The step.</returns>
        private PlannedStep StepAt(int index)
        {
            if (index >= _system.Stages.Count)
                return _end;

            SystemStage stage = _system.Stages[index];
            switch (stage.Kind)
            {
                case SystemStageKind.Process:
                    return new PlannedStep(PlannedStepKind.Process, stage.Process, null, _noRoutes);

                case SystemStageKind.Shop:
                    return new PlannedStep(PlannedStepKind.Shop, null, stage.Shop, _noRoutes);

                default:
                    return new PlannedStep(PlannedStepKind.RouteSelection, null, null, stage.Routes);
            }
        }

        /// <summary>
        /// Finds the authored route choice that offers a route.
        /// </summary>
        /// <param name="route">The route.</param>
        /// <returns>The stage index, or -1 when no choice offers it.</returns>
        private int IndexOfChoiceOffering(RouteID route)
        {
            IReadOnlyList<SystemStage> stages = _system.Stages;
            for (int index = 0; index < stages.Count; index++)
            {
                if (stages[index].Kind == SystemStageKind.RouteSelection && Contains(stages[index].Routes, route))
                    return index;
            }

            return -1;
        }

        /// <summary>
        /// Whether a route list holds a route.
        /// </summary>
        /// <param name="routes">The list.</param>
        /// <param name="route">The route.</param>
        /// <returns>True when present.</returns>
        private static bool Contains(IReadOnlyList<RouteID> routes, RouteID route)
        {
            for (int index = 0; index < routes.Count; index++)
            {
                if (routes[index] == route)
                    return true;
            }

            return false;
        }
    }
}