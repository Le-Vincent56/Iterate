using System;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Applies Branch configuration: includes and excludes edit the draft, and a confirmation locks
    /// the Branch and enters the Process. A refused confirmation stays in the stage.
    /// </summary>
    internal sealed class BranchCommandHandler
    {
        private const ChangedAreas EntryAreas = ChangedAreas.Branch | ChangedAreas.Build | ChangedAreas.Buffer | ChangedAreas.Bytes | ChangedAreas.Counters;
        private readonly StageTransitions _transitions;

        /// <summary>
        /// Creates the handler over the stage transitions.
        /// </summary>
        /// <param name="transitions">The transitions.</param>
        internal BranchCommandHandler(StageTransitions transitions)
        {
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        }

        /// <summary>
        /// Includes, excludes, or confirms.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="ConfigureBranch"/> command.</param>
        /// <returns>The edit result, the confirmation result, or the refused Process creation.</returns>
        /// <exception cref="ArgumentException">Thrown when an include or exclude names no item, or a confirmation names one.</exception>
        internal HandlerOutcome Configure(SessionContext context, ISessionCommand command)
        {
            ConfigureBranch configure = (ConfigureBranch)command;
            if (configure.Action == BranchAction.Confirm)
                return Confirm(context, configure);

            if (!configure.Item.HasValue)
                throw new ArgumentException("An include or exclude names the item it acts on.", nameof(command));

            ActiveBranch branch = context.Session.ActiveBranch;
            InstanceID item = configure.Item.Value;
            BranchEditResult edited = configure.Action == BranchAction.Include ? branch.Include(item) : branch.Exclude(item);
            return new HandlerOutcome(edited.Succeeded, edited, edited.Succeeded ? ChangedAreas.Branch : ChangedAreas.None);
        }

        /// <summary>
        /// Confirms the Branch and enters the Process the plan stands on.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="configure">The confirmation.</param>
        /// <returns>The confirmation result, or the refused Process creation.</returns>
        private HandlerOutcome Confirm(SessionContext context, ConfigureBranch configure)
        {
            if (configure.Item.HasValue)
                throw new ArgumentException("A confirmation names no item.", nameof(configure));

            BranchConfirmationResult confirmed = context.Session.ActiveBranch.Confirm();
            if (!confirmed.Succeeded)
                return new HandlerOutcome(false, confirmed, ChangedAreas.None);

            ProcessConfigurationDefinition configuration = StageTransitions.ConfigurationOf(context, context.Plan.Current.Process.Value);
            ProcessCreationResult created = _transitions.EnterProcess(context, configuration);
            if (!created.Succeeded)
                return new HandlerOutcome(false, created, ChangedAreas.None);

            return new HandlerOutcome(true, confirmed, EntryAreas);
        }
    }
}