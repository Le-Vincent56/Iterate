using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The Session's economy: its Tokens, its installed Dependencies and RAM, the commitments a
    /// purchase can leave behind, and every committed transaction in order. Holds the register every
    /// economy operation reads its tunables from, so no transaction carries a literal price.
    /// </summary>
    public sealed class EconomyState
    {
        private readonly InstanceIDSource _instanceIDs;
        private readonly List<UtilityCommitment> _utilities = new();
        private readonly List<PatchGrant> _grants = new();
        private readonly List<TransactionRecord> _records = new();
        private readonly Dictionary<TransactionKind, int> _transactionCounts = new();

        /// <summary>
        /// The Session's Token ledger.
        /// </summary>
        public TokenLedger Tokens { get; }

        /// <summary>
        /// The Session's installed Dependencies and RAM.
        /// </summary>
        public DependencyRack Dependencies { get; }

        /// <summary>
        /// The locked parameter register every economy tunable is read from.
        /// </summary>
        public ParameterSet Parameters { get; }

        /// <summary>
        /// The Utilities bought and bound to a Process, in commitment order.
        /// </summary>
        public IReadOnlyList<UtilityCommitment> Utilities => _utilities;

        /// <summary>
        /// The Patch grants awaiting a host, in grant order.
        /// </summary>
        public IReadOnlyList<PatchGrant> PatchGrants => _grants;

        /// <summary>
        /// Every committed transaction, in order.
        /// </summary>
        public IReadOnlyList<TransactionRecord> Records => _records;

        public EconomyState(InstanceIDSource instanceIDs, DependencyInstance starter, ParameterSet parameters)
        {
            _instanceIDs = instanceIDs ?? throw new ArgumentException("An economy requires an identity source.", nameof(instanceIDs));
            Parameters = parameters ?? throw new ArgumentException("An economy requires a parameter register.", nameof(parameters));
            Tokens = new TokenLedger(new TokenAmount(0));
            Dependencies = new DependencyRack(starter, parameters.StartingRAM);
        }

        /// <summary>
        /// Allocates the next identity for a transaction kind.
        /// </summary>
        /// <param name="kind">The transaction kind.</param>
        /// <returns>The per-kind identity, "kind:n".</returns>
        public string NextTransactionIdentity(TransactionKind kind)
        {
            int next = _transactionCounts.TryGetValue(kind, out int used) ? used + 1 : 1;
            _transactionCounts[kind] = next;
            return KindToken(kind) + ":" + next;
        }

        /// <summary>
        /// Records a committed transaction.
        /// </summary>
        /// <param name="record">The transaction record.</param>
        public void Append(TransactionRecord record)
        {
            if (record == null)
                throw new ArgumentException("A transaction record is required.", nameof(record));

            _records.Add(record);
        }

        /// <summary>
        /// Commits a bought Utility to one Process. The same Utility definition may be committed to
        /// two different Processes, but not twice to one.
        /// </summary>
        /// <param name="definition">The frozen Utility definition.</param>
        /// <param name="target">The Process it applies to.</param>
        /// <param name="offerID">The offer that sold it.</param>
        /// <returns>The commitment result.</returns>
        public UtilityCommitmentResult CommitUtility(UtilityDefinition definition, ProcessID target, string offerID)
        {
            if (definition == null)
                throw new ArgumentException("A commitment requires a Utility definition.", nameof(definition));

            for (int i = 0; i < _utilities.Count; i++)
            {
                if (_utilities[i].Definition.ID == definition.ID && _utilities[i].TargetProcess == target)
                    return new UtilityCommitmentResult(false, true, null);
            }

            UtilityCommitment commitment = new(_instanceIDs.Next(), definition, target, offerID);
            _utilities.Add(commitment);
            return new UtilityCommitmentResult(true, false, commitment);
        }

        /// <summary>
        /// The process-setup effects committed for a Process. Reading does not consume: a commitment
        /// ends when the Process does, not when its effects are read, so a resolver may be re-run.
        /// </summary>
        /// <param name="process">The Process being set up.</param>
        /// <returns>The active setup effects, in commitment then declaration order.</returns>
        public IReadOnlyList<ActiveSetupEffect> SetupEffectsFor(ProcessID process)
        {
            List<ActiveSetupEffect> effects = new List<ActiveSetupEffect>();
            for (int i = 0; i < _utilities.Count; i++)
            {
                UtilityCommitment commitment = _utilities[i];
                if (commitment.TargetProcess != process)
                    continue;

                IReadOnlyList<EffectDefinition> declared = commitment.Definition.Effects;
                for (int j = 0; j < declared.Count; j++)
                {
                    if (declared[j].PhaseDomain != PhaseDomain.ProcessSetup)
                        continue;

                    effects.Add(new ActiveSetupEffect(
                        commitment.Definition.ID.Value,
                        commitment.Definition.DisplayName,
                        declared[j]));
                }
            }

            return effects;
        }

        /// <summary>
        /// Ends every commitment bound to a Process.
        /// </summary>
        /// <param name="process">The Process that has finished.</param>
        /// <returns>How many commitments ended.</returns>
        public int ExpireUtilitiesFor(ProcessID process)
        {
            int removed = 0;
            for (int i = _utilities.Count - 1; i >= 0; i--)
            {
                if (_utilities[i].TargetProcess != process)
                    continue;

                _utilities.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// Mints a Patch grant.
        /// </summary>
        /// <param name="definition">The frozen Patch definition granted.</param>
        /// <param name="cached">Whether the grant may be held indefinitely.</param>
        /// <param name="basis">The reward component it came from.</param>
        /// <returns>The grant.</returns>
        public PatchGrant Grant(PatchDefinition definition, bool cached, string basis)
        {
            if (definition == null)
                throw new ArgumentException("A grant requires a Patch definition.", nameof(definition));

            PatchGrant grant = new(_instanceIDs.Next(), definition, cached, basis);
            _grants.Add(grant);
            return grant;
        }

        /// <summary>
        /// Finds an outstanding grant by identity.
        /// </summary>
        /// <param name="grant">The grant identity.</param>
        /// <param name="entry">The grant found, or null.</param>
        /// <returns>True when the grant is outstanding.</returns>
        public bool TryGetGrant(InstanceID grant, out PatchGrant entry)
        {
            for (int i = 0; i < _grants.Count; i++)
            {
                if (_grants[i].GrantIdentity == grant)
                {
                    entry = _grants[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Consumes an outstanding grant.
        /// </summary>
        /// <param name="grant">The grant identity.</param>
        /// <returns>True when a grant was consumed; false when none was outstanding.</returns>
        public bool ConsumeGrant(InstanceID grant)
        {
            for (int i = 0; i < _grants.Count; i++)
            {
                if (_grants[i].GrantIdentity != grant)
                    continue;

                _grants.RemoveAt(i);
                return true;
            }

            return false;
        }

        /// <summary>
        /// The lowercase token a transaction identity is built from.
        /// </summary>
        /// <param name="kind">The transaction kind.</param>
        /// <returns>The token.</returns>
        private static string KindToken(TransactionKind kind)
        {
            switch (kind)
            {
                case TransactionKind.Purchase: return "purchase";
                case TransactionKind.Reroll: return "reroll";
                case TransactionKind.PatchAttachment: return "patch-attachment";
                case TransactionKind.DependencyInstallation: return "dependency-installation";
                case TransactionKind.DependencyDestruction: return "dependency-destruction";
                case TransactionKind.ItemDeletion: return "item-deletion";
                case TransactionKind.ItemDuplication: return "item-duplication";
                case TransactionKind.Reward: return "reward";
                default:
                    throw new ArgumentException("Unknown transaction kind " + kind + ".", nameof(kind));
            }
        }
    }
}