using System;
using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The complete decision evidence: the declaration echo, the exact candidate snapshot evidence
    /// (identities, canonical order, weights), the revision identity, the
    /// ordered per-draw records, the first-class full permutation for shuffle and ordering decisions
    /// (empty for the four selection methods), and the outcome. Equality is structural over the lists and
    /// nested records so a recomputed record compares equal to the original.
    /// </summary>
    public sealed record RandomDecisionRecord
    {
        /// <summary>
        /// The declaration this decision was made from.
        /// </summary>
        public DecisionRequest Request { get; }

        /// <summary>
        /// The exact captured candidate evidence in canonical order.
        /// </summary>
        public IReadOnlyList<CandidateEntry> CandidateEvidence { get; }

        /// <summary>
        /// The random-service revision identity stamped on the record.
        /// </summary>
        public string RevisionIdentity { get; }

        /// <summary>
        /// The ordered per-draw records.
        /// </summary>
        public IReadOnlyList<DrawRecord> Draws { get; }

        /// <summary>
        /// The full permutation for a shuffle or ordering decision; empty for the four selection methods.
        /// </summary>
        public IReadOnlyList<string> ResultPermutation { get; }

        /// <summary>
        /// The resolved outcome.
        /// </summary>
        public DecisionOutcome Outcome { get; }

        public RandomDecisionRecord(
            DecisionRequest request,
            IReadOnlyList<CandidateEntry> candidateEvidence,
            string revisionIdentity,
            IReadOnlyList<DrawRecord> draws,
            IReadOnlyList<string> resultPermutation,
            DecisionOutcome outcome
        )
        {
            if (string.IsNullOrEmpty(revisionIdentity))
                throw new ArgumentException("A decision record requires a revision identity.", nameof(revisionIdentity));

            Request = request ?? throw new ArgumentException("A decision record requires a request.", nameof(request));
            CandidateEvidence = candidateEvidence ?? throw new ArgumentException("A decision record requires candidate evidence.", nameof(candidateEvidence));
            RevisionIdentity = revisionIdentity;
            Draws = draws ?? throw new ArgumentException("A decision record requires a draws list.", nameof(draws));
            ResultPermutation = resultPermutation ?? throw new ArgumentException("A decision record requires a result-permutation list.", nameof(resultPermutation));
            Outcome = outcome ?? throw new ArgumentException("A decision record requires an outcome.", nameof(outcome));
        }

        /// <summary>
        /// Structural value equality over the request, revision identity, evidence, draws, permutation, and
        /// outcome.
        /// </summary>
        /// <param name="other">The record to compare against.</param>
        /// <returns>True when every field is equal.</returns>
        public bool Equals(RandomDecisionRecord other)
        {
            if (other == null) return false;

            return string.Equals(RevisionIdentity, other.RevisionIdentity, StringComparison.Ordinal)
                && Request == other.Request
                && Outcome == other.Outcome
                && DeterminismEquality.ListEquals(CandidateEvidence, other.CandidateEvidence)
                && DeterminismEquality.ListEquals(Draws, other.Draws)
                && DeterminismEquality.ListEquals(ResultPermutation, other.ResultPermutation);
        }

        /// <summary>
        /// A hash consistent with structural equality, over list sizes and the outcome hash.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + CandidateEvidence.Count;
                hash = hash * 31 + Draws.Count;
                hash = hash * 31 + ResultPermutation.Count;
                hash = hash * 31 + Outcome.GetHashCode();
                return hash;
            }
        }
    }
}