using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// An applied reward and what it still owes. The immediates are already committed; the pending
    /// choices and the outstanding grants are what keep it incomplete. Completeness is read live from
    /// the Session rather than snapshotted, so attaching a granted Patch anywhere completes it.
    /// </summary>
    public sealed class RewardResolution
    {
        private readonly SessionState _session;
        private readonly ContentCatalog _catalog;
        private readonly List<PendingReward> _pending;
        private readonly List<InstanceID> _grants;

        /// <summary>
        /// The plan this resolution applied.
        /// </summary>
        public RewardPlan Plan { get; }

        /// <summary>
        /// The choices still owed, in authored order.
        /// </summary>
        public IReadOnlyList<PendingReward> Pending => _pending;

        /// <summary>
        /// Every grant this resolution created, cached ones included.
        /// </summary>
        public IReadOnlyList<InstanceID> Grants => _grants;

        /// <summary>
        /// The transaction the immediates committed under.
        /// </summary>
        public TransactionRecord Record { get; }

        /// <summary>
        /// Whether nothing is still owed: no choice pending, and every non-cached grant spent. A
        /// cached grant may be held indefinitely, so it never holds a resolution open.
        /// </summary>
        public bool IsComplete
        {
            get
            {
                if (_pending.Count > 0)
                    return false;

                for (int i = 0; i < _grants.Count; i++)
                {
                    if (!_session.Economy.TryGetGrant(_grants[i], out PatchGrant grant))
                        continue;

                    if (!grant.Cached)
                        return false;
                }

                return true;
            }
        }

        public RewardResolution(
            RewardPlan plan,
            SessionState session,
            ContentCatalog catalog,
            List<PendingReward> pending,
            List<InstanceID> grants,
            TransactionRecord record
        )
        {
            Plan = plan;
            _session = session;
            _catalog = catalog;
            _pending = pending;
            _grants = grants;
            Record = record;
        }

        /// <summary>
        /// Takes one pending pool choice. A Dependency member installs at price zero through the
        /// installer, so RAM is validated exactly as a purchase would be and a refused install leaves
        /// the choice pending rather than consuming it.
        /// </summary>
        /// <param name="componentIndex">The pending component's authored index.</param>
        /// <param name="contentID">The chosen member's content ID.</param>
        /// <param name="destroyFirst">Dependencies to destroy first, for a Dependency choice.</param>
        /// <returns>The choice result.</returns>
        public RewardChoiceResult Choose(int componentIndex, string contentID, IReadOnlyList<InstanceID> destroyFirst)
        {
            int index = IndexOfPending(componentIndex);
            if (index < 0)
                return new RewardChoiceResult(false, RewardRejection.ComponentNotPending, null, DependencyRejection.None);

            PendingReward pending = _pending[index];
            if (!IsMember(pending.Pool, contentID))
                return new RewardChoiceResult(false, RewardRejection.NotAPoolMember, null, DependencyRejection.None);

            List<TransactionConsequence> consequences = new();
            if (contentID.StartsWith("WB-DEP-", StringComparison.Ordinal))
            {
                if (!_catalog.TryGetDependency(new DependencyID(contentID), out DependencyDefinition dependency))
                    return new RewardChoiceResult(false, RewardRejection.ContentMissing, null, DependencyRejection.None);

                DependencyInstallationResult installed = new DependencyInstaller(_session).Install(
                    new DependencyInstallationRequest(
                        dependency,
                        0,
                        DependencyOrigin.Reward,
                        destroyFirst ?? Array.Empty<InstanceID>(),
                        Plan.Package.ID.Value + ":" + componentIndex));

                if (!installed.Succeeded)
                    return new RewardChoiceResult(false, RewardRejection.InstallationRejected, null, installed.Rejection);

                consequences.Add(new RewardComponentResolved(componentIndex, contentID));
                _pending.RemoveAt(index);
                return new RewardChoiceResult(true, RewardRejection.None, installed.Record, DependencyRejection.None);
            }

            if (!_catalog.TryGetItem(contentID, out ContentDefinition item))
                return new RewardChoiceResult(false, RewardRejection.ContentMissing, null, DependencyRejection.None);

            AcquisitionResult acquired = _session.Repository.Acquire(item, AcquisitionOrigin.Reward);
            consequences.Add(new ItemAcquired(acquired.Entry.Item.InstanceID, contentID, AcquisitionOrigin.Reward));
            consequences.Add(new RewardComponentResolved(componentIndex, contentID));

            TransactionRecord record = new(
                TransactionKind.Reward,
                _session.Economy.NextTransactionIdentity(TransactionKind.Reward),
                consequences);
            _session.Economy.Append(record);
            _pending.RemoveAt(index);
            return new RewardChoiceResult(true, RewardRejection.None, record, DependencyRejection.None);
        }

        /// <summary>
        /// Finds a pending choice by its authored component index.
        /// </summary>
        /// <param name="componentIndex">The authored index.</param>
        /// <returns>Its position in the pending list, or -1.</returns>
        private int IndexOfPending(int componentIndex)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].ComponentIndex == componentIndex)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Whether a pool holds a content ID.
        /// </summary>
        /// <param name="pool">The pool.</param>
        /// <param name="contentID">The content ID.</param>
        /// <returns>True when the pool holds it.</returns>
        private static bool IsMember(PoolDefinition pool, string contentID)
        {
            IReadOnlyList<PoolMember> members = pool.Members;
            for (int i = 0; i < members.Count; i++)
            {
                if (string.Equals(members[i].Content, contentID, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}