using System;
using Iterate.Domain.Progression;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Resolves a submitted Process's rewards. The stage blocks until every choice is made and every
    /// granted Patch is attached, because a Patch has no unattached state. The last resolution moves
    /// the Session to its next step.
    /// </summary>
    internal sealed class RewardCommandHandler
    {
        private const ChangedAreas RewardAreas = ChangedAreas.Rewards | ChangedAreas.Economy | ChangedAreas.Repository;
        private readonly StageTransitions _transitions;

        /// <summary>
        /// Creates the handler over the stage transitions.
        /// </summary>
        /// <param name="transitions">The transitions.</param>
        internal RewardCommandHandler(StageTransitions transitions)
        {
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        }

        /// <summary>
        /// Resolves one reward component by choosing one of its options.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="SelectReward"/> command.</param>
        /// <returns>The choice result.</returns>
        internal HandlerOutcome Select(SessionContext context, ISessionCommand command)
        {
            SelectReward select = (SelectReward)command;
            RewardChoiceResult chosen = context.Rewards.Choose(select.ComponentIndex, select.ContentID, select.DestroyFirst);
            if (!chosen.Succeeded)
                return new HandlerOutcome(false, chosen, ChangedAreas.None);

            return new HandlerOutcome(true, chosen, RewardAreas | _transitions.FinishRewardsIfResolved(context));
        }

        /// <summary>
        /// Attaches a granted Patch to a host socket. The attachment is free and carries the grant's
        /// own basis as its reference.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="InstallPatch"/> command.</param>
        /// <returns>The attachment result.</returns>
        /// <exception cref="ArgumentException">Thrown when the command names no grant or names a slot.</exception>
        internal HandlerOutcome AttachGrant(SessionContext context, ISessionCommand command)
        {
            InstallPatch install = (InstallPatch)command;
            if (!install.Grant.HasValue || install.Slot.HasValue)
                throw new ArgumentException("A granted Patch attachment names a grant and no slot.", nameof(command));

            SessionState session = context.Session;
            if (!session.Economy.TryGetGrant(install.Grant.Value, out PatchGrant grant))
                return new HandlerOutcome(false, new PatchAttachmentResult(false, PatchAttachmentRejection.UnknownGrant, null, null), ChangedAreas.None);

            PatchAttachmentRequest request = new(
                grant.Definition,
                install.Host,
                install.Socket,
                0,
                grant.GrantIdentity,
                grant.Basis
            );

            PatchAttachmentResult attached = new PatchInstaller(session).Attach(request);
            if (!attached.Succeeded)
                return new HandlerOutcome(false, attached, ChangedAreas.None);

            return new HandlerOutcome(true, attached, RewardAreas | _transitions.FinishRewardsIfResolved(context));
        }
    }
}