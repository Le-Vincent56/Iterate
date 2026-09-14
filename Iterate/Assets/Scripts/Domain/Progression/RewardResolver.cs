using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Resolves a reward package in two phases. Plan is pure and total: every granted component
    /// resolves to a concrete consequence or the whole package is rejected, so a package naming
    /// content the catalog cannot supply credits nothing. Apply then commits what Plan resolved, in
    /// authored order, and cannot fail on content because Plan already found all of it.
    /// </summary>
    public static class RewardResolver
    {
        /// <summary>
        /// Resolves what a package awards at the tier reached, without awarding any of it.
        /// </summary>
        /// <param name="package">The package to resolve.</param>
        /// <param name="reached">The tier reached, or null for none.</param>
        /// <param name="session">The Session the reward would be applied to.</param>
        /// <param name="catalog">The frozen catalog.</param>
        /// <returns>The planning result.</returns>
        public static RewardPlanResult Plan(
            RewardPackageDefinition package,
            RewardTier? reached,
            SessionState session,
            ContentCatalog catalog
        )
        {
            if (package == null)
                throw new ArgumentException("Planning requires a package.", nameof(package));

            List<PlannedReward> planned = new();
            if (!reached.HasValue)
                return new RewardPlanResult(true, RewardRejection.None, new RewardPlan(package, null, planned));

            IReadOnlyList<RewardComponent> components = package.Components;
            for (int i = 0; i < components.Count; i++)
            {
                RewardComponent component = components[i];
                if (component.Tier > reached.Value)
                    continue;

                switch (component.Kind)
                {
                    case RewardComponentKind.Tokens:
                        planned.Add(new PlannedReward(
                            i,
                            component,
                            PlannedRewardKind.TokenCredit,
                            component.Tier == RewardTier.Benchmark ? TokenBasis.BenchmarkBonus : TokenBasis.RewardTokens,
                            component.Amount.Value,
                            null,
                            null,
                            false
                        ));
                        break;

                    case RewardComponentKind.RouteTokenBonus:
                        planned.Add(new PlannedReward(
                            i,
                            component,
                            PlannedRewardKind.TokenCredit,
                            TokenBasis.RouteBonus,
                            component.Amount.Value,
                            null,
                            null,
                            false
                        ));
                        break;

                    case RewardComponentKind.RAMSet:
                        if (component.Amount.Value < session.Economy.Dependencies.Usage)
                            return new RewardPlanResult(false, RewardRejection.CapacityBelowUsage, null);

                        planned.Add(new PlannedReward(
                            i, 
                            component, 
                            PlannedRewardKind.CapacityTarget,
                            null,
                            component.Amount.Value,
                            null,
                            null,
                            false
                        ));
                        break;

                    case RewardComponentKind.GuaranteedContent:
                        if (!catalog.TryGetItem(component.Reference, out ContentDefinition content))
                            return new RewardPlanResult(false, RewardRejection.ContentMissing, null);

                        planned.Add(new PlannedReward(
                            i, component, PlannedRewardKind.GuaranteedContent, null, 0, content, null, false));
                        break;

                    case RewardComponentKind.Patch:
                    case RewardComponentKind.CachedPatch:
                        if (!catalog.TryGetPatch(new PatchID(component.Reference), out PatchDefinition patch))
                            return new RewardPlanResult(false, RewardRejection.ContentMissing, null);

                        planned.Add(new PlannedReward(
                            i, 
                            component,
                            PlannedRewardKind.PatchGrant,
                            null,
                            0,
                            patch,
                            null,
                            component.Kind == RewardComponentKind.CachedPatch
                        ));
                        break;

                    case RewardComponentKind.PoolChoice:
                        if (!catalog.TryGetPool(new PoolID(component.Reference), out PoolDefinition pool))
                            return new RewardPlanResult(false, RewardRejection.PoolMissing, null);

                        if (pool.Members.Count == 0)
                            return new RewardPlanResult(false, RewardRejection.PoolEmpty, null);

                        planned.Add(new PlannedReward(
                            i,
                            component,
                            PlannedRewardKind.PoolChoice,
                            null,
                            0,
                            null,
                            pool,
                            false
                        ));
                        break;

                    default:
                        throw new ArgumentException(
                            "The reward component kind " + component.Kind + " is not planned.",
                            nameof(package)
                        );
                }
            }

            return new RewardPlanResult(true, RewardRejection.None, new RewardPlan(package, reached, planned));
        }

        /// <summary>
        /// Commits a plan's immediate consequences in authored order and returns what is still owed.
        /// </summary>
        /// <param name="plan">The resolved plan.</param>
        /// <param name="session">The Session to award into.</param>
        /// <param name="catalog">The frozen catalog, for the choices still to be taken.</param>
        /// <returns>The resolution.</returns>
        public static RewardResolution Apply(RewardPlan plan, SessionState session, ContentCatalog catalog)
        {
            if (plan == null)
                throw new ArgumentException("Applying requires a plan.", nameof(plan));

            List<TransactionConsequence> consequences = new();
            List<PendingReward> pending = new();
            List<InstanceID> grants = new();

            IReadOnlyList<PlannedReward> planned = plan.Planned;
            for (int i = 0; i < planned.Count; i++)
            {
                PlannedReward entry = planned[i];
                string reference = plan.Package.ID.Value + ":" + entry.ComponentIndex;
                switch (entry.Kind)
                {
                    case PlannedRewardKind.TokenCredit:
                        TokenLedgerResult credit = session.Economy.Tokens.Credit(
                            new TokenAmount(entry.Amount),
                            entry.Basis.Value,
                            reference
                        );
                        consequences.Add(new TokenConsequence(new TokenAmount(entry.Amount), false, entry.Basis.Value, credit.Balance));
                        break;

                    case PlannedRewardKind.CapacityTarget:
                        session.Economy.Dependencies.SetCapacity(entry.Amount);
                        consequences.Add(new RewardComponentResolved(entry.ComponentIndex, "RAM " + entry.Amount));
                        break;

                    case PlannedRewardKind.GuaranteedContent:
                        AcquisitionResult acquired = session.Repository.Acquire(entry.Content, AcquisitionOrigin.Reward);
                        consequences.Add(new ItemAcquired(
                            acquired.Entry.Item.InstanceID,
                            acquired.Entry.Item.DefinitionID,
                            AcquisitionOrigin.Reward
                        ));
                        break;

                    case PlannedRewardKind.PatchGrant:
                        PatchGrant grant = session.Economy.Grant((PatchDefinition)entry.Content, entry.Cached, reference);
                        grants.Add(grant.GrantIdentity);
                        consequences.Add(new PatchGranted(grant.GrantIdentity, entry.Content.DisplayName, entry.Cached));
                        break;

                    case PlannedRewardKind.PoolChoice:
                        pending.Add(new PendingReward(entry.ComponentIndex, entry.Component, entry.Pool));
                        break;
                }
            }

            TransactionRecord record = new(
                TransactionKind.Reward,
                session.Economy.NextTransactionIdentity(TransactionKind.Reward),
                consequences
            );
            session.Economy.Append(record);
            return new RewardResolution(plan, session, catalog, pending, grants, record);
        }
    }
}