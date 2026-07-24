using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The per-execution effect engine: stores the interpreted effects sorted by stable instance
    /// identity at registration — so registration and enumeration order cannot influence resolution —
    /// and matches typed occurrences at the scheduler's seven boundaries, each candidate gated by its
    /// participation kind. Failed candidacies follow one tiering everywhere: a candidate whose
    /// occurrence shape cannot legally qualify it is silent, a consumed allowance is silent — except
    /// a selected-host modification, which re-applies without re-consumption to later offers hosted
    /// by its recorded instance — and an eligible candidate that fails a requirement produces exactly
    /// one near-miss naming the first failure, including the origin lock. Qualified effects are
    /// ordered by declared precedence, then stable instance identity; qualified added-execution
    /// creators are collected separately, awaiting the scheduler's commitment.
    /// </summary>
    public sealed class EffectEngine
    {
        /// <summary>
        /// The near-miss requirement prefix naming an origin-lock block.
        /// </summary>
        private const string OriginLockRequirement = "ORIGIN_LOCK:";

        private readonly List<ActiveEffect> _registered;
        private readonly FrequencyLedger _ledger;
        private readonly Dictionary<string, InstanceID> _selectedHosts;

        /// <summary>
        /// The registered effects, sorted by owning instance identity then effect index.
        /// </summary>
        public IReadOnlyList<ActiveEffect> RegisteredEffects => _registered;

        public EffectEngine(IReadOnlyList<ActiveEffect> effects, FrequencyLedger ledger)
        {
            if (effects == null)
                throw new ArgumentException("An effect engine requires an effect list.", nameof(effects));

            _ledger = ledger ?? throw new ArgumentException("An effect engine requires a frequency ledger.", nameof(ledger));

            _registered = new List<ActiveEffect>(effects.Count);
            for (int i = 0; i < effects.Count; i++)
            {
                InsertSorted(_registered, effects[i]);
            }

            _selectedHosts = new Dictionary<string, InstanceID>();
        }

        /// <summary>
        /// Matches modification-band effects against a pending-operation occurrence. A consumed
        /// selected-host modification whose recorded host matches the occurrence re-applies when its
        /// qualifiers pass; when they fail it stays silent — consumed effects never produce
        /// near-miss noise.
        /// </summary>
        /// <param name="occurrence">The pending-operation occurrence.</param>
        /// <returns>The captured batch, or the shared empty batch.</returns>
        /// <exception cref="ArgumentException">Thrown when the occurrence is null.</exception>
        public EffectMatchBatch MatchPendingOperation(OperationOccurrence occurrence)
        {
            if (occurrence == null)
                throw new ArgumentException("Matching requires an occurrence.", nameof(occurrence));

            return MatchOperation(occurrence, ActiveEffectKind.Modification, ExecutionEventSubtypes.PrimaryOperationPending);
        }

        /// <summary>
        /// Matches reaction-band effects against a resolved-operation occurrence.
        /// </summary>
        /// <param name="occurrence">The resolved-operation occurrence.</param>
        /// <returns>The captured batch, or the shared empty batch.</returns>
        /// <exception cref="ArgumentException">Thrown when the occurrence is null.</exception>
        public EffectMatchBatch MatchResolvedOperation(OperationOccurrence occurrence)
        {
            if (occurrence == null)
                throw new ArgumentException("Matching requires an occurrence.", nameof(occurrence));

            return MatchOperation(occurrence, ActiveEffectKind.Reaction, ExecutionEventSubtypes.PrimaryOperationResolved);
        }

        /// <summary>
        /// Matches reaction-band effects and added-execution creators against a finalized
        /// quantity-change occurrence. Creators observe source-execution events only: a change that
        /// is not the unit's primary operation cannot qualify one, silently — its targeting has no
        /// declared meaning against an effect-caused event.
        /// </summary>
        /// <param name="occurrence">The quantity-change occurrence.</param>
        /// <returns>The captured batch, or the shared empty batch.</returns>
        /// <exception cref="ArgumentException">Thrown when the occurrence is null.</exception>
        public EffectMatchBatch MatchQuantityChange(QuantityOccurrence occurrence)
        {
            if (occurrence == null)
                throw new ArgumentException("Matching requires an occurrence.", nameof(occurrence));

            List<ActiveEffect> qualified = null;
            List<EffectNearMiss> nearMisses = null;
            List<ActiveEffect> creators = null;

            for (int i = 0; i < _registered.Count; i++)
            {
                ActiveEffect effect = _registered[i];
                bool isCreator = effect.Kind == ActiveEffectKind.AddedExecution;
                if (effect.Kind != ActiveEffectKind.Reaction && !isCreator)
                    continue;

                if (effect.Trigger.EventSubtype != ExecutionEventSubtypes.QuantityChanged)
                    continue;

                if (isCreator && !occurrence.FromPrimaryOperation)
                    continue;

                if (!_ledger.IsEligible(effect))
                    continue;

                if (isCreator && occurrence.BranchLineage.Contains(effect.Origin))
                {
                    nearMisses ??= new List<EffectNearMiss>();
                    nearMisses.Add(new EffectNearMiss(effect, OriginLockRequirement + effect.Origin.ToString()));
                    continue;
                }

                TriggerQualifier failed = FirstFailedQuantityQualifier(effect, occurrence);
                if (failed != null)
                {
                    nearMisses ??= new List<EffectNearMiss>();
                    nearMisses.Add(new EffectNearMiss(effect, failed.Kind + ":" + failed.Value));
                    continue;
                }

                if (isCreator)
                {
                    creators ??= new List<ActiveEffect>();
                    InsertQualified(creators, effect);
                    continue;
                }

                qualified ??= new List<ActiveEffect>();
                InsertQualified(qualified, effect);
            }

            return ToBatch(qualified, nearMisses, null, creators);
        }

        /// <summary>
        /// Matches added-execution creators against a closed runtime unit at the post-unit band. An
        /// unsuccessful closure cannot qualify a creator, silently — canon requires a successfully
        /// resolving unit, so an unsuccessful one was never a candidate.
        /// </summary>
        /// <param name="occurrence">The post-unit occurrence.</param>
        /// <returns>The captured batch, or the shared empty batch.</returns>
        /// <exception cref="ArgumentException">Thrown when the occurrence is null.</exception>
        public EffectMatchBatch MatchPostUnit(PostUnitOccurrence occurrence)
        {
            if (occurrence == null)
                throw new ArgumentException("Matching requires an occurrence.", nameof(occurrence));

            if (occurrence.FinalDisposition != EventDisposition.Resolved && occurrence.FinalDisposition != EventDisposition.Rescued)
                return EffectMatchBatch.Empty;

            List<EffectNearMiss> nearMisses = null;
            List<ActiveEffect> creators = null;

            for (int i = 0; i < _registered.Count; i++)
            {
                ActiveEffect effect = _registered[i];
                if (effect.Kind != ActiveEffectKind.AddedExecution || effect.Trigger.EventSubtype != ExecutionEventSubtypes.RuntimeUnitCompleted)
                    continue;

                if (!_ledger.IsEligible(effect))
                    continue;

                if (occurrence.BranchLineage.Contains(effect.Origin))
                {
                    nearMisses ??= new List<EffectNearMiss>();
                    nearMisses.Add(new EffectNearMiss(effect, OriginLockRequirement + effect.Origin.ToString()));
                    continue;
                }

                TriggerQualifier failed = FirstFailedPostUnitQualifier(effect, occurrence);
                if (failed != null)
                {
                    nearMisses ??= new List<EffectNearMiss>();
                    nearMisses.Add(new EffectNearMiss(effect, failed.Kind + ":" + failed.Value));
                    continue;
                }

                creators ??= new List<ActiveEffect>();
                InsertQualified(creators, effect);
            }

            return ToBatch(null, nearMisses, null, creators);
        }

        /// <summary>
        /// Matches added-execution creators against a successful Condition evaluation at the
        /// post-unit band. An evaluation with no occupied child offers no legal host, so no creator
        /// was ever a candidate — silent, and nothing is consumed.
        /// </summary>
        /// <param name="occurrence">The Condition-success occurrence.</param>
        /// <returns>The captured batch, or the shared empty batch.</returns>
        /// <exception cref="ArgumentException">Thrown when the occurrence is null.</exception>
        public EffectMatchBatch MatchConditionSuccess(ConditionSuccessOccurrence occurrence)
        {
            if (occurrence == null)
                throw new ArgumentException("Matching requires an occurrence.", nameof(occurrence));

            if (occurrence.FirstOccupiedChild == null)
                return EffectMatchBatch.Empty;

            List<EffectNearMiss> nearMisses = null;
            List<ActiveEffect> creators = null;

            for (int i = 0; i < _registered.Count; i++)
            {
                ActiveEffect effect = _registered[i];
                if (effect.Kind != ActiveEffectKind.AddedExecution || effect.Trigger.EventSubtype != ExecutionEventSubtypes.ConditionTrue)
                    continue;

                if (!_ledger.IsEligible(effect))
                    continue;

                if (occurrence.BranchLineage.Contains(effect.Origin))
                {
                    nearMisses ??= new List<EffectNearMiss>();
                    nearMisses.Add(new EffectNearMiss(effect, OriginLockRequirement + effect.Origin.ToString()));
                    continue;
                }

                creators ??= new List<ActiveEffect>();
                InsertQualified(creators, effect);
            }

            return ToBatch(null, nearMisses, null, creators);
        }

        /// <summary>
        /// Matches boundary effects declaring the reached boundary against its register snapshot.
        /// Boundary effects resolve in place; they never create requests.
        /// </summary>
        /// <param name="occurrence">The boundary occurrence.</param>
        /// <returns>The captured batch, or the shared empty batch.</returns>
        /// <exception cref="ArgumentException">Thrown when the occurrence is null.</exception>
        public EffectMatchBatch MatchBoundary(BoundaryOccurrence occurrence)
        {
            if (occurrence == null)
                throw new ArgumentException("Matching requires an occurrence.", nameof(occurrence));

            List<ActiveEffect> qualified = null;
            List<EffectNearMiss> nearMisses = null;

            for (int i = 0; i < _registered.Count; i++)
            {
                ActiveEffect effect = _registered[i];
                if (effect.Kind != ActiveEffectKind.Boundary || effect.BoundaryName != occurrence.BoundaryName)
                    continue;

                if (!_ledger.IsEligible(effect))
                    continue;

                TriggerQualifier failed = FirstFailedBoundaryQualifier(effect, occurrence);
                if (failed != null)
                {
                    nearMisses ??= new List<EffectNearMiss>();
                    nearMisses.Add(new EffectNearMiss(effect, failed.Kind + ":" + failed.Value));
                    continue;
                }

                qualified ??= new List<ActiveEffect>();
                InsertQualified(qualified, effect);
            }

            return ToBatch(qualified, nearMisses, null, null);
        }

        /// <summary>
        /// Matches rescue-kind effects against a skipped source-execution occurrence at the
        /// pre-operation band. A non-rescuable occurrence and a ledger-ineligible rescue are both
        /// silent — no candidate, no near-miss. The rescue trigger pair carries no qualifiers
        /// (interpreter-enforced), so a matched eligible rescue always qualifies.
        /// </summary>
        /// <param name="occurrence">The skip occurrence.</param>
        /// <returns>The captured batch, or the shared empty batch.</returns>
        /// <exception cref="ArgumentException">Thrown when the occurrence is null.</exception>
        public EffectMatchBatch MatchSkip(SkipOccurrence occurrence)
        {
            if (occurrence == null)
                throw new ArgumentException("Matching requires an occurrence.", nameof(occurrence));

            if (!occurrence.Rescuable)
                return EffectMatchBatch.Empty;

            List<ActiveEffect> qualified = null;
            for (int i = 0; i < _registered.Count; i++)
            {
                ActiveEffect effect = _registered[i];
                if (effect.Kind != ActiveEffectKind.Rescue || effect.Trigger.EventSubtype != ExecutionEventSubtypes.SourceExecutionSkipped)
                    continue;

                if (!_ledger.IsEligible(effect))
                    continue;

                qualified ??= new List<ActiveEffect>();
                InsertQualified(qualified, effect);
            }

            return ToBatch(qualified, null, null, null);
        }

        /// <summary>
        /// Consumes the effect's frequency allowance — commitment is the only consumption point.
        /// </summary>
        /// <param name="effect">The committing effect.</param>
        /// <exception cref="ArgumentException">Thrown when the effect is null.</exception>
        public void Commit(ActiveEffect effect)
        {
            _ledger.Consume(effect);
        }

        /// <summary>
        /// Consumes a modification's frequency allowance exactly like <see cref="Commit"/>, then
        /// records the committing occurrence's host instance as the effect's selected host when the
        /// effect is a selected-host modification with a non-null host.
        /// </summary>
        /// <param name="effect">The committing modification effect.</param>
        /// <param name="hostInstance">The committing occurrence's host instance, or null.</param>
        /// <exception cref="ArgumentException">Thrown when the effect is null.</exception>
        public void CommitModification(ActiveEffect effect, InstanceID? hostInstance)
        {
            _ledger.Consume(effect);

            if (effect.Kind == ActiveEffectKind.Modification
                && SelectedHostEffects.IsSelectedHost(effect.DefinitionID)
                && hostInstance != null)
            {
                _selectedHosts[effect.FrequencyKey] = hostInstance.Value;
            }
        }

        /// <summary>
        /// Drops all registered state and recorded selected hosts — the execution-expiration cleanup.
        /// </summary>
        public void Clear()
        {
            _registered.Clear();
            _selectedHosts.Clear();
        }

        /// <summary>
        /// Matches operation-boundary effects of one participation kind against an operation
        /// occurrence. At the modification band, a ledger-ineligible selected-host effect whose
        /// recorded host equals the occurrence's host re-applies when its qualifiers pass and stays
        /// silent when they fail — the eligibility exception's guard.
        /// </summary>
        /// <param name="occurrence">The operation occurrence.</param>
        /// <param name="wantKind">The participation kind this boundary offers.</param>
        /// <param name="subtype">The trigger subtype this boundary offers.</param>
        /// <returns>The captured batch, or the shared empty batch.</returns>
        private EffectMatchBatch MatchOperation(
            OperationOccurrence occurrence,
            ActiveEffectKind wantKind,
            string subtype
        )
        {
            List<ActiveEffect> qualified = null;
            List<EffectNearMiss> nearMisses = null;
            List<ActiveEffect> reapplications = null;

            for (int i = 0; i < _registered.Count; i++)
            {
                ActiveEffect effect = _registered[i];
                if (effect.Kind != wantKind || effect.Trigger.EventSubtype != subtype)
                    continue;

                if (!_ledger.IsEligible(effect))
                {
                    if (wantKind == ActiveEffectKind.Modification
                        && SelectedHostEffects.IsSelectedHost(effect.DefinitionID)
                        && _selectedHosts.TryGetValue(effect.FrequencyKey, out InstanceID recordedHost)
                        && occurrence.HostInstance == recordedHost
                        && FirstFailedOperationQualifier(effect, occurrence) == null)
                    {
                        reapplications ??= new List<ActiveEffect>();
                        InsertQualified(reapplications, effect);
                    }

                    continue;
                }

                TriggerQualifier failed = FirstFailedOperationQualifier(effect, occurrence);
                if (failed != null)
                {
                    nearMisses ??= new List<EffectNearMiss>();
                    nearMisses.Add(new EffectNearMiss(effect, failed.Kind + ":" + failed.Value));
                    continue;
                }

                qualified ??= new List<ActiveEffect>();
                InsertQualified(qualified, effect);
            }

            return ToBatch(qualified, nearMisses, reapplications, null);
        }

        /// <summary>
        /// Returns the first qualifier the operation occurrence fails, or null when all pass.
        /// </summary>
        /// <param name="effect">The candidate effect.</param>
        /// <param name="occurrence">The operation occurrence.</param>
        /// <returns>The first failing qualifier, or null.</returns>
        private static TriggerQualifier FirstFailedOperationQualifier(ActiveEffect effect, OperationOccurrence occurrence)
        {
            IReadOnlyList<TriggerQualifier> qualifiers = effect.Trigger.Qualifiers;
            for (int i = 0; i < qualifiers.Count; i++)
            {
                if (!EvaluateOperationQualifier(qualifiers[i], occurrence))
                    return qualifiers[i];
            }

            return null;
        }

        /// <summary>
        /// Returns the first qualifier the quantity occurrence fails, or null when all pass.
        /// </summary>
        /// <param name="effect">The candidate effect.</param>
        /// <param name="occurrence">The quantity occurrence.</param>
        /// <returns>The first failing qualifier, or null.</returns>
        private static TriggerQualifier FirstFailedQuantityQualifier(ActiveEffect effect, QuantityOccurrence occurrence)
        {
            IReadOnlyList<TriggerQualifier> qualifiers = effect.Trigger.Qualifiers;
            for (int i = 0; i < qualifiers.Count; i++)
            {
                if (!EvaluateQuantityQualifier(qualifiers[i], occurrence))
                    return qualifiers[i];
            }

            return null;
        }

        /// <summary>
        /// Returns the first qualifier the post-unit occurrence fails, or null when all pass.
        /// </summary>
        /// <param name="effect">The candidate effect.</param>
        /// <param name="occurrence">The post-unit occurrence.</param>
        /// <returns>The first failing qualifier, or null.</returns>
        private static TriggerQualifier FirstFailedPostUnitQualifier(ActiveEffect effect, PostUnitOccurrence occurrence)
        {
            IReadOnlyList<TriggerQualifier> qualifiers = effect.Trigger.Qualifiers;
            for (int i = 0; i < qualifiers.Count; i++)
            {
                if (!EvaluatePostUnitQualifier(qualifiers[i], occurrence))
                    return qualifiers[i];
            }

            return null;
        }

        /// <summary>
        /// Returns the first qualifier the boundary occurrence fails, or null when all pass.
        /// </summary>
        /// <param name="effect">The candidate effect.</param>
        /// <param name="occurrence">The boundary occurrence.</param>
        /// <returns>The first failing qualifier, or null.</returns>
        private static TriggerQualifier FirstFailedBoundaryQualifier(ActiveEffect effect, BoundaryOccurrence occurrence)
        {
            IReadOnlyList<TriggerQualifier> qualifiers = effect.Trigger.Qualifiers;
            for (int i = 0; i < qualifiers.Count; i++)
            {
                if (!EvaluateBoundaryQualifier(qualifiers[i], effect.Trigger.Qualifiers, occurrence))
                    return qualifiers[i];
            }

            return null;
        }

        /// <summary>
        /// Evaluates one qualifier against an operation occurrence's structural facts. Tokens that are
        /// not facts of an operation evaluate false; unknown tokens are unreachable behind the
        /// interpreter's closed vocabulary.
        /// </summary>
        /// <param name="qualifier">The qualifier to evaluate.</param>
        /// <param name="occurrence">The operation occurrence.</param>
        /// <returns>True when the occurrence satisfies the qualifier.</returns>
        private static bool EvaluateOperationQualifier(TriggerQualifier qualifier, OperationOccurrence occurrence)
        {
            switch (qualifier.Kind)
            {
                case "REGISTER":
                    return RegisterToken(occurrence.Register) == qualifier.Value;

                case "OPERATION_CLASS":
                    switch (qualifier.Value)
                    {
                        case "FIXED_ADDITION":
                            return occurrence.Operator == CoreLineOperator.Add && occurrence.OperandSource == OperandSource.Constant;

                        case "VALUE_ADD_SIGNAL":
                            return occurrence.Register == CoreRegister.Value
                                && occurrence.Operator == CoreLineOperator.Add
                                && occurrence.OperandSource == OperandSource.Register
                                && occurrence.OperandRegister == CoreRegister.Signal;

                        case "PLAYER_INSTRUCTION":
                            return occurrence.Ownership == OwnershipClassification.PlayerOwned;

                        default:
                            return false;
                    }

                default:
                    return false;
            }
        }

        /// <summary>
        /// Evaluates one qualifier against a quantity occurrence's finalized facts. Tokens that are
        /// not facts of a quantity change evaluate false; unknown tokens are unreachable behind the
        /// interpreter's closed vocabulary.
        /// </summary>
        /// <param name="qualifier">The qualifier to evaluate.</param>
        /// <param name="occurrence">The quantity occurrence.</param>
        /// <returns>True when the occurrence satisfies the qualifier.</returns>
        private static bool EvaluateQuantityQualifier(TriggerQualifier qualifier, QuantityOccurrence occurrence)
        {
            switch (qualifier.Kind)
            {
                case "REGISTER":
                    return RegisterToken(occurrence.Register) == qualifier.Value;

                case "ACTUAL_DELTA_SIGN":
                    return qualifier.Value == "POSITIVE" && occurrence.ActualDelta > 0;

                case "OPERATION_CLASS":
                    return qualifier.Value == "PLAYER_INSTRUCTION"
                        && occurrence.FromPrimaryOperation
                        && occurrence.Ownership == OwnershipClassification.PlayerOwned;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Evaluates one qualifier against a closed unit's retained context. The Repeat-context
        /// qualifier passes only when the unit executed inside an active Repeat iteration.
        /// </summary>
        /// <param name="qualifier">The qualifier to evaluate.</param>
        /// <param name="occurrence">The post-unit occurrence.</param>
        /// <returns>True when the occurrence satisfies the qualifier.</returns>
        private static bool EvaluatePostUnitQualifier(TriggerQualifier qualifier, PostUnitOccurrence occurrence)
        {
            if (qualifier.Kind != "STRUCTURE_CONTEXT" || qualifier.Value != "INSIDE_REPEAT")
                return false;

            return occurrence.StructureContext != null && occurrence.StructureContext.RepeatIterationIdentity != null;
        }

        /// <summary>
        /// Evaluates one qualifier against a boundary's register snapshot. The register qualifier
        /// names what the boundary reads and always passes; the parity qualifier tests that named
        /// register's snapshot.
        /// </summary>
        /// <param name="qualifier">The qualifier to evaluate.</param>
        /// <param name="declared">The effect's full qualifier list, for resolving the named register.</param>
        /// <param name="occurrence">The boundary occurrence.</param>
        /// <returns>True when the occurrence satisfies the qualifier.</returns>
        private static bool EvaluateBoundaryQualifier(
            TriggerQualifier qualifier,
            IReadOnlyList<TriggerQualifier> declared,
            BoundaryOccurrence occurrence
        )
        {
            switch (qualifier.Kind)
            {
                case "REGISTER":
                    return true;

                case "PARITY":
                    if (qualifier.Value != "ODD")
                        return false;

                    int snapshot = SnapshotOf(DeclaredRegisterToken(declared), occurrence);
                    return snapshot % 2 != 0;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns the register token the effect's qualifier list names, or the Value token when none
        /// is declared — the interpreter requires a register qualifier alongside parity, so the
        /// fallback is unreachable for parity evaluation.
        /// </summary>
        /// <param name="declared">The effect's qualifier list.</param>
        /// <returns>The declared register token.</returns>
        private static string DeclaredRegisterToken(IReadOnlyList<TriggerQualifier> declared)
        {
            for (int i = 0; i < declared.Count; i++)
            {
                if (declared[i].Kind == "REGISTER")
                    return declared[i].Value;
            }

            return "VALUE";
        }

        /// <summary>
        /// Returns the boundary snapshot of the named register.
        /// </summary>
        /// <param name="registerToken">The controlled register token.</param>
        /// <param name="occurrence">The boundary occurrence.</param>
        /// <returns>The named register's snapshot.</returns>
        /// <exception cref="ArgumentException">Thrown when the token is not a known register.</exception>
        private static int SnapshotOf(string registerToken, BoundaryOccurrence occurrence)
        {
            switch (registerToken)
            {
                case "VALUE":
                    return occurrence.Value;

                case "SIGNAL":
                    return occurrence.Signal;

                case "SCORE":
                    return occurrence.Score;

                default:
                    throw new ArgumentException($"Unknown register token {registerToken}.", nameof(registerToken));
            }
        }

        /// <summary>
        /// Returns the controlled register token for a register.
        /// </summary>
        /// <param name="register">The register to name.</param>
        /// <returns>The controlled token.</returns>
        /// <exception cref="ArgumentException">Thrown when the register is not a known member.</exception>
        private static string RegisterToken(CoreRegister register)
        {
            switch (register)
            {
                case CoreRegister.Value:
                    return "VALUE";

                case CoreRegister.Signal:
                    return "SIGNAL";

                case CoreRegister.Score:
                    return "SCORE";

                default:
                    throw new ArgumentException($"Unknown register {register}.", nameof(register));
            }
        }

        /// <summary>
        /// Inserts an effect into the registry sorted by owning instance identity then effect index.
        /// </summary>
        /// <param name="registered">The registry list.</param>
        /// <param name="effect">The effect to insert.</param>
        private static void InsertSorted(List<ActiveEffect> registered, ActiveEffect effect)
        {
            int index = registered.Count;
            while (index > 0 && CompareIdentity(registered[index - 1], effect) > 0)
            {
                index--;
            }

            registered.Insert(index, effect);
        }

        /// <summary>
        /// Inserts a qualified effect sorted by declared precedence rank, then instance identity.
        /// </summary>
        /// <param name="qualified">The qualified list.</param>
        /// <param name="effect">The effect to insert.</param>
        private static void InsertQualified(List<ActiveEffect> qualified, ActiveEffect effect)
        {
            int index = qualified.Count;
            while (index > 0 && CompareQualified(qualified[index - 1], effect) > 0)
            {
                index--;
            }

            qualified.Insert(index, effect);
        }

        /// <summary>
        /// Compares two effects by stable instance identity: owning instance, then effect index.
        /// </summary>
        /// <param name="left">The left effect.</param>
        /// <param name="right">The right effect.</param>
        /// <returns>The comparison result.</returns>
        private static int CompareIdentity(ActiveEffect left, ActiveEffect right)
        {
            int byInstance = left.Origin.Value.CompareTo(right.Origin.Value);
            if (byInstance != 0)
                return byInstance;

            return left.EffectIndex.CompareTo(right.EffectIndex);
        }

        /// <summary>
        /// Compares two qualified effects by declared precedence rank, then stable instance identity.
        /// </summary>
        /// <param name="left">The left effect.</param>
        /// <param name="right">The right effect.</param>
        /// <returns>The comparison result.</returns>
        private static int CompareQualified(ActiveEffect left, ActiveEffect right)
        {
            int byRank = ReactionPrecedence.Rank(left.DefinitionID).CompareTo(ReactionPrecedence.Rank(right.DefinitionID));
            if (byRank != 0)
                return byRank;

            return CompareIdentity(left, right);
        }

        /// <summary>
        /// Wraps the collected lists in a batch, or returns the shared empty batch when nothing
        /// qualified, near-missed, re-applied, or created.
        /// </summary>
        /// <param name="qualified">The qualified effects, or null.</param>
        /// <param name="nearMisses">The near-misses, or null.</param>
        /// <param name="reapplications">The selected-host re-applications, or null.</param>
        /// <param name="creators">The qualified added-execution creators, or null.</param>
        /// <returns>The batch.</returns>
        private static EffectMatchBatch ToBatch(
            List<ActiveEffect> qualified,
            List<EffectNearMiss> nearMisses,
            List<ActiveEffect> reapplications,
            List<ActiveEffect> creators
        )
        {
            if (qualified == null && nearMisses == null && reapplications == null && creators == null)
                return EffectMatchBatch.Empty;

            return new EffectMatchBatch(
                qualified ?? (IReadOnlyList<ActiveEffect>)Array.Empty<ActiveEffect>(),
                nearMisses ?? (IReadOnlyList<EffectNearMiss>)Array.Empty<EffectNearMiss>(),
                reapplications ?? (IReadOnlyList<ActiveEffect>)Array.Empty<ActiveEffect>(),
                creators ?? (IReadOnlyList<ActiveEffect>)Array.Empty<ActiveEffect>()
            );
        }
    }
}