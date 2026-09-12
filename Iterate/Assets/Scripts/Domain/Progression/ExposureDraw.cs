using System;
using System.Collections.Generic;
using System.Globalization;
using Iterate.Domain.Determinism;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The Process-scoped exposure draw: one Determinism decision per arrival position over the
    /// confirmed Active Branch's unseen, non-guaranteed population. The position counter starts at one
    /// for every draw and the Session ordinal tracker is never consulted, which is what makes an
    /// in-Session retry reproduce its arrivals without any reset machinery. The candidate
    /// snapshot is captured at the moment of each draw, so an item removed between draws simply
    /// changes the next snapshot (CAB-EVT-873).
    /// </summary>
    public sealed class ExposureDraw
    {
        /// <summary>
        /// The declared selection purpose, a member of <see cref="SelectionPurposes.All"/>.
        /// </summary>
        public const string Purpose = "Active Branch exposure";

        private const string ScopeIdentity = "PROCESS";

        private const string SelectionBoundaryIdentity = "Instruction Buffer arrival moment";

        private const string CandidateSourceIdentity = "confirmed Active Branch, unseen, non-guaranteed";

        private const string EligibilityRuleIdentity = "Branch-eligible and not yet exposed this Process";

        private const string SnapshotTimingIdentity = "at the arrival moment";

        private const string OrderingRuleIdentity = "canonical definition-then-instance";

        private const string TieBreakRuleIdentity = "canonical instance identity";

        private readonly DeterminismService _service = new();

        private readonly List<RandomDecisionRecord> _records = new();

        private readonly ActiveBranchConfiguration _branch;

        private readonly Repository _repository;

        private readonly List<InstanceID> _exposed;

        private readonly string _sessionSeedIdentity;

        private readonly string _catalogRevision;

        private readonly string _systemIdentity;

        private readonly string _processIdentity;

        private int _nextPosition = 1;

        /// <summary>
        /// The decision records this draw has produced, in draw order, cancellations included.
        /// </summary>
        public IReadOnlyList<RandomDecisionRecord> Records => _records;

        /// <summary>
        /// The position the next draw will consume; one before any draw has occurred.
        /// </summary>
        public int NextPosition => _nextPosition;

        public ExposureDraw(
            ActiveBranchConfiguration branch,
            Repository repository,
            List<InstanceID> exposed,
            SessionState session,
            string processIdentity
        )
        {
            if (branch == null) throw new ArgumentNullException(nameof(branch));
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (exposed == null) throw new ArgumentNullException(nameof(exposed));
            if (session == null) throw new ArgumentNullException(nameof(session));

            _branch = branch;
            _repository = repository;
            _exposed = exposed;
            _sessionSeedIdentity = session.SessionSeedIdentity;
            _catalogRevision = session.CatalogRevision;
            _systemIdentity = session.SystemIdentity;
            _processIdentity = processIdentity;
        }

        /// <summary>
        /// Resolves a guaranteed definition to the lowest-suffix Branch entry not already exposed. This
        /// runs before any draw and marks nothing itself; the caller records the exposure, so guaranteed
        /// content leaves every later random denominator (CAB-EVT-865).
        /// </summary>
        /// <param name="definitionID">The guaranteed content's definition identity.</param>
        /// <param name="entry">The resolved Repository entry, or null.</param>
        /// <returns>True when the confirmed Branch holds an unexposed entry of that definition.</returns>
        public bool TryResolveGuaranteed(string definitionID, out RepositoryEntry entry)
        {
            IReadOnlyList<RepositoryEntry> entries = _repository.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                RepositoryEntry candidate = entries[index];
                if (!string.Equals(candidate.Item.DefinitionID, definitionID, StringComparison.Ordinal))
                    continue;

                if (!IsEligible(candidate.Item.InstanceID))
                    continue;

                entry = candidate;
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Draws the next arrival: captures the unseen population at this moment, resolves one uniform
        /// single selection, retains the record, and marks the selected instance exposed. A cancelled
        /// decision admits nothing and still consumes its position.
        /// </summary>
        /// <returns>The drawn item and its record, or the cancelled record.</returns>
        public ExposureDrawResult DrawNext()
        {
            int position = _nextPosition;
            _nextPosition += 1;

            List<RepositoryEntry> population = EligiblePopulation();
            List<CandidateEntry> candidates = new(population.Count);
            for (int index = 0; index < population.Count; index++)
            {
                RepositoryItem item = population[index].Item;
                string identity = item.InstanceID.ToString();
                candidates.Add(new CandidateEntry(
                    identity,
                    new CandidateOrderingKey(item.DefinitionID, identity, null, null),
                    null
                ));
            }

            DecisionResult result = _service.Decide(
                BuildRequest(position),
                CandidateSnapshot.Create(candidates)
            );
            _records.Add(result.Record);

            if (result.Outcome.Disposition != DecisionDisposition.Selected)
                return ExposureDrawResult.Cancelled(result.Record);

            RepositoryEntry selected = EntryOf(population, result.Outcome.SelectedIdentities[0]);
            _exposed.Add(selected.Item.InstanceID);
            return ExposureDrawResult.Drawn(selected.Item, result.Record);
        }

        /// <summary>
        /// Builds the decision declaration for one draw position.
        /// </summary>
        /// <param name="position">The draw position, from one.</param>
        /// <returns>The complete request.</returns>
        private DecisionRequest BuildRequest(int position)
        {
            DecisionContextComponents components = new(
                _sessionSeedIdentity,
                _catalogRevision,
                DeterminismService.RevisionIdentity,
                _systemIdentity,
                _processIdentity,
                ScopeIdentity,
                null,
                null,
                Purpose,
                position
            );

            return new DecisionRequest(
                _processIdentity + ":exposure:" + position.ToString(CultureInfo.InvariantCulture),
                Purpose,
                SelectionBoundaryIdentity,
                CandidateSourceIdentity,
                EligibilityRuleIdentity,
                SnapshotTimingIdentity,
                OrderingRuleIdentity,
                1,
                SelectionMethod.UniformSingleSelection,
                ReplacementBehavior.RemovedFromLongerLivedPopulation,
                null,
                TieBreakRuleIdentity,
                null,
                InsufficientCandidateBehavior.CancelTheDecision,
                DecisionContext.Derive(components)
            );
        }

        /// <summary>
        /// The Branch entries still unseen this Process, in Repository acquisition order. The snapshot
        /// sorts them into canonical order itself, so this order is capture order only.
        /// </summary>
        /// <returns>The eligible population.</returns>
        private List<RepositoryEntry> EligiblePopulation()
        {
            List<RepositoryEntry> population = new();
            IReadOnlyList<RepositoryEntry> entries = _repository.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                if (IsEligible(entries[index].Item.InstanceID))
                    population.Add(entries[index]);
            }

            return population;
        }

        /// <summary>
        /// Whether an instance is in the confirmed Branch and has not been exposed this Process.
        /// </summary>
        /// <param name="id">The instance identity.</param>
        /// <returns>True when the instance may be drawn.</returns>
        private bool IsEligible(InstanceID id)
        {
            if (Holds(_exposed, id))
                return false;

            return Holds(_branch.Eligible, id);
        }

        /// <summary>
        /// Whether a list of instance identities holds the given one.
        /// </summary>
        /// <param name="ids">The identities to search.</param>
        /// <param name="id">The identity to find.</param>
        /// <returns>True when present.</returns>
        private static bool Holds(IReadOnlyList<InstanceID> ids, InstanceID id)
        {
            for (int index = 0; index < ids.Count; index++)
            {
                if (ids[index].Equals(id))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves a selected candidate identity back to the entry it was captured from, so no site
        /// has to parse an identity string back into an instance identity.
        /// </summary>
        /// <param name="population">The population the snapshot was captured from.</param>
        /// <param name="identity">The selected candidate identity.</param>
        /// <returns>The matching entry.</returns>
        private static RepositoryEntry EntryOf(List<RepositoryEntry> population, string identity)
        {
            for (int index = 0; index < population.Count; index++)
            {
                if (string.Equals(population[index].Item.InstanceID.ToString(), identity, StringComparison.Ordinal))
                    return population[index];
            }

            throw new InvalidOperationException("A selected candidate must come from the captured population.");
        }
    }
}