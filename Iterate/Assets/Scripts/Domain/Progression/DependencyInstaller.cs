using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Installs and destroys Dependencies. An installation may destroy others first, and the whole
    /// composite is validated against the state those destructions would leave — released RAM and
    /// refunded Tokens included — before anything commits. A refused composite destroys nothing, which
    /// is what keeps a rejection from costing the player a Dependency it never replaced.
    /// </summary>
    public sealed class DependencyInstaller
    {
        private readonly SessionState _session;

        public DependencyInstaller(SessionState session)
        {
            _session = session ?? throw new ArgumentException("An installer requires a Session.", nameof(session));
        }

        /// <summary>
        /// The refund for destroying a Dependency bought at a price: half, rounded down. The floor is
        /// applied once, to the product, so an odd price refunds the lower whole Token.
        /// </summary>
        /// <param name="pricePaid">The Tokens originally paid.</param>
        /// <returns>The refund in Tokens.</returns>
        public int RefundFor(int pricePaid)
        {
            return (int)Math.Floor(_session.Economy.Parameters.DependencyDestructionRefundRatio * pricePaid);
        }

        /// <summary>
        /// What an installation would do, without doing it.
        /// </summary>
        /// <param name="request">The installation request.</param>
        /// <returns>The preview.</returns>
        public DependencyInstallationPreview Preview(DependencyInstallationRequest request)
        {
            return Validate(request, out List<InstalledDependency> _);
        }

        /// <summary>
        /// Installs a Dependency, first destroying any the request names. Commit order is the canonical
        /// one: each destruction releases its RAM and credits its refund, then the price is debited,
        /// then the new Dependency reserves its RAM — so the refund is spendable on the price it funds.
        /// </summary>
        /// <param name="request">The installation request.</param>
        /// <returns>The installation result.</returns>
        public DependencyInstallationResult Install(DependencyInstallationRequest request)
        {
            DependencyInstallationPreview preview = Validate(request, out List<InstalledDependency> targets);
            if (preview.Rejection != DependencyRejection.None)
                return new DependencyInstallationResult(false, preview.Rejection, null, null);

            List<TransactionConsequence> consequences = new List<TransactionConsequence>();
            for (int i = 0; i < targets.Count; i++)
            {
                Destroy(targets[i], consequences);
            }

            if (request.Price > 0)
            {
                TokenLedgerResult debit = _session.Economy.Tokens.Debit(
                    new TokenAmount(request.Price),
                    TokenBasis.OfferPrice,
                    request.Reference
                );
                consequences.Add(new TokenConsequence(new TokenAmount(request.Price), true, TokenBasis.OfferPrice, debit.Balance));
            }

            RackResult installed = _session.Economy.Dependencies.Install(
                request.Dependency,
                _session.InstanceIDs.Next(),
                request.Price,
                request.Origin);

            consequences.Add(new DependencyInstalled(
                installed.Entry.Instance.InstanceID,
                request.Dependency.ID.Value,
                request.Price
            ));
            consequences.Add(new RAMReservation(
                installed.Entry.Instance.InstanceID,
                installed.RAMChanged,
                _session.Economy.Dependencies.Usage
            ));

            TransactionRecord record = new(
                TransactionKind.DependencyInstallation,
                _session.Economy.NextTransactionIdentity(TransactionKind.DependencyInstallation),
                consequences
            );
            _session.Economy.Append(record);
            return new DependencyInstallationResult(true, DependencyRejection.None, record, installed.Entry);
        }

        /// <summary>
        /// What destroying a Dependency would return, without doing it.
        /// </summary>
        /// <param name="dependency">The instance to destroy.</param>
        /// <returns>The preview.</returns>
        public DependencyDestructionPreview PreviewDestroy(InstanceID dependency)
        {
            if (_session.Economy.Dependencies.Starter.InstanceID == dependency)
                return new DependencyDestructionPreview(DependencyRejection.StarterPermanent, 0, 0, 0, _session.Economy.Tokens.Balance);

            if (!_session.Economy.Dependencies.TryGet(dependency, out InstalledDependency entry))
                return new DependencyDestructionPreview(DependencyRejection.UnknownInstance, 0, 0, 0, _session.Economy.Tokens.Balance);

            int refund = RefundFor(entry.PricePaid);
            return new DependencyDestructionPreview(
                DependencyRejection.None,
                entry.Instance.Definition.RAM,
                entry.PricePaid,
                refund,
                new TokenAmount(_session.Economy.Tokens.Balance.Value + refund)
            );
        }

        /// <summary>
        /// Destroys one installed Dependency, releasing its RAM and refunding half its price.
        /// </summary>
        /// <param name="dependency">The instance to destroy.</param>
        /// <returns>The destruction result.</returns>
        public DependencyDestructionResult Destroy(InstanceID dependency)
        {
            DependencyDestructionPreview preview = PreviewDestroy(dependency);
            if (preview.Rejection != DependencyRejection.None)
                return new DependencyDestructionResult(false, preview.Rejection, null, 0);

            _session.Economy.Dependencies.TryGet(dependency, out InstalledDependency entry);
            List<TransactionConsequence> consequences = new List<TransactionConsequence>();
            Destroy(entry, consequences);

            TransactionRecord record = new(
                TransactionKind.DependencyDestruction,
                _session.Economy.NextTransactionIdentity(TransactionKind.DependencyDestruction),
                consequences
            );
            _session.Economy.Append(record);
            return new DependencyDestructionResult(true, DependencyRejection.None, record, preview.Refund);
        }

        /// <summary>
        /// Commits one destruction into a transaction under construction: release the RAM, then credit
        /// the refund. Shared by the standalone destruction and the composite installation so both
        /// produce the same consequence shape.
        /// </summary>
        /// <param name="entry">The installation to destroy.</param>
        /// <param name="consequences">The consequences being accumulated.</param>
        private void Destroy(InstalledDependency entry, List<TransactionConsequence> consequences)
        {
            int refund = RefundFor(entry.PricePaid);
            RackResult released = _session.Economy.Dependencies.Destroy(entry.Instance.InstanceID);

            consequences.Add(new DependencyDestroyed(
                entry.Instance.InstanceID,
                entry.Instance.Definition.ID.Value,
                refund)
            );
            consequences.Add(new RAMRelease(
                entry.Instance.InstanceID,
                released.RAMChanged,
                _session.Economy.Dependencies.Usage)
            );

            if (refund > 0)
            {
                TokenLedgerResult credit = _session.Economy.Tokens.Credit(
                    new TokenAmount(refund),
                    TokenBasis.DestructionRefund,
                    entry.Instance.Definition.ID.Value
                );
                consequences.Add(new TokenConsequence(new TokenAmount(refund), false, TokenBasis.DestructionRefund, credit.Balance));
            }
        }

        /// <summary>
        /// The one validation both entry points run: the definition is a Dependency, every destroy
        /// target resolves and is removable and named once, the RAM fits after the releases, and the
        /// Tokens cover the price after the refunds.
        /// </summary>
        /// <param name="request">The installation request.</param>
        /// <param name="targets">The resolved destruction targets, in request order.</param>
        /// <returns>The preview describing the request's outcome.</returns>
        private DependencyInstallationPreview Validate(
            DependencyInstallationRequest request,
            out List<InstalledDependency> targets
        )
        {
            targets = new List<InstalledDependency>();
            if (request == null)
                throw new ArgumentException("An installation requires a request.", nameof(request));

            DependencyRack rack = _session.Economy.Dependencies;
            TokenAmount before = _session.Economy.Tokens.Balance;
            int usage = rack.Usage;
            if (request.Dependency == null || request.Dependency.Category != ContentCategory.Dependency)
                return Refused(DependencyRejection.NotADependency, usage, rack.Capacity, before);

            int freed = 0;
            int refunds = 0;
            IReadOnlyList<InstanceID> destroyFirst = request.DestroyFirst;
            for (int i = 0; i < destroyFirst.Count; i++)
            {
                InstanceID target = destroyFirst[i];
                if (rack.Starter.InstanceID == target)
                    return Refused(DependencyRejection.StarterPermanent, usage, rack.Capacity, before);

                if (!rack.TryGet(target, out InstalledDependency entry))
                    return Refused(DependencyRejection.UnknownInstance, usage, rack.Capacity, before);

                if (NamesTarget(targets, target))
                    return Refused(DependencyRejection.DuplicateDestroyTarget, usage, rack.Capacity, before);

                targets.Add(entry);
                freed += entry.Instance.Definition.RAM;
                refunds += RefundFor(entry.PricePaid);
            }

            int after = usage - freed + request.Dependency.RAM;
            if (after > rack.Capacity)
            {
                targets = new List<InstalledDependency>();
                return new DependencyInstallationPreview(
                    DependencyRejection.InsufficientRAM,
                    usage,
                    freed,
                    after,
                    rack.Capacity,
                    refunds,
                    request.Price,
                    before,
                    before
                );
            }

            if (before.Value + refunds < request.Price)
            {
                targets = new List<InstalledDependency>();
                return new DependencyInstallationPreview(
                    DependencyRejection.InsufficientTokens,
                    usage,
                    freed,
                    after,
                    rack.Capacity,
                    refunds,
                    request.Price,
                    before,
                    before
                );
            }

            return new DependencyInstallationPreview(
                DependencyRejection.None,
                usage, 
                freed,
                after,
                rack.Capacity,
                refunds,
                request.Price,
                before,
                new TokenAmount(before.Value + refunds - request.Price)
            );
        }

        /// <summary>
        /// Whether a target is already among the resolved destruction targets.
        /// </summary>
        /// <param name="targets">The targets resolved so far.</param>
        /// <param name="candidate">The candidate identity.</param>
        /// <returns>True when it is already named.</returns>
        private static bool NamesTarget(List<InstalledDependency> targets, InstanceID candidate)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].Instance.InstanceID == candidate)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Builds a preview for a request refused before its RAM and Token effects could be computed.
        /// </summary>
        /// <param name="rejection">Why it was refused.</param>
        /// <param name="usage">The unchanged usage.</param>
        /// <param name="capacity">The rack capacity.</param>
        /// <param name="before">The unchanged balance.</param>
        /// <returns>The refusing preview.</returns>
        private static DependencyInstallationPreview Refused(
            DependencyRejection rejection,
            int usage,
            int capacity,
            TokenAmount before
        )
        {
            return new DependencyInstallationPreview(
                rejection, 
                usage,
                0, 
                usage,
                capacity,
                0,
                0,
                before,
                before
            );
        }
    }
}