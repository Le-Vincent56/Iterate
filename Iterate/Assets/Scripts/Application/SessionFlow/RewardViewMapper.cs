using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Copies the rewards still to resolve into a <see cref="RewardView"/>.
    /// </summary>
    internal static class RewardViewMapper
    {
        /// <summary>
        /// Maps the context's reward resolution.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The view, or null outside the rewards stage.</returns>
        internal static RewardView Map(SessionContext context)
        {
            RewardResolution rewards = context.Rewards;
            if (rewards == null || context.Stage != SessionStage.Rewards)
                return null;

            IReadOnlyList<PendingReward> pending = rewards.Pending;
            List<PendingRewardView> pendingViews = new(pending.Count);
            for (int index = 0; index < pending.Count; index++)
                pendingViews.Add(ViewOf(pending[index]));

            List<InstanceID> openGrants = new();
            IReadOnlyList<InstanceID> grants = rewards.Grants;
            for (int index = 0; index < grants.Count; index++)
            {
                if (context.Session.Economy.TryGetGrant(grants[index], out PatchGrant _))
                    openGrants.Add(grants[index]);
            }

            return new RewardView(rewards.IsComplete, pendingViews, openGrants);
        }

        /// <summary>
        /// Copies one pending component with the content its pool offers.
        /// </summary>
        /// <param name="pending">The pending component.</param>
        /// <returns>The component's view.</returns>
        private static PendingRewardView ViewOf(PendingReward pending)
        {
            List<string> options = new();
            if (pending.Pool != null)
            {
                IReadOnlyList<PoolMember> members = pending.Pool.Members;
                for (int index = 0; index < members.Count; index++)
                    options.Add(members[index].Content);
            }

            return new PendingRewardView(pending.ComponentIndex, pending.Component.Kind, options);
        }
    }
}