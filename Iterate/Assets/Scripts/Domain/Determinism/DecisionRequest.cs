using System;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The declaration of a random decision, complete by construction — an unconstructible
    /// request is how "silence must not allow technical implementation to choose" is implemented. The
    /// boundary, source, eligibility, ordering, tie-break, and weighting-rule fields are declarative
    /// identity strings recorded into evidence; this leaf records what the caller declared. Two
    /// consistency rules bind at construction: the count rule (shuffle = 0, others >= 1) and
    /// method × replacement consistency.
    /// </summary>
    public sealed record DecisionRequest
    {
        /// <summary>
        /// The stable identity of this selection decision.
        /// </summary>
        public string SelectionIdentity { get; }

        /// <summary>
        /// The declared selection purpose; a member of <see cref="SelectionPurposes.All"/>.
        /// </summary>
        public string SelectionPurpose { get; }

        /// <summary>
        /// The declared selection boundary identity.
        /// </summary>
        public string SelectionBoundary { get; }

        /// <summary>
        /// The declared candidate-source identity.
        /// </summary>
        public string CandidateSource { get; }

        /// <summary>
        /// The declared eligibility-rule identity.
        /// </summary>
        public string EligibilityRule { get; }

        /// <summary>
        /// The declared candidate-snapshot-timing identity.
        /// </summary>
        public string CandidateSnapshotTiming { get; }

        /// <summary>
        /// The declared candidate stable-ordering-rule identity.
        /// </summary>
        public string CandidateOrderingRule { get; }

        /// <summary>
        /// The declared selection count; zero for <see cref="SelectionMethod.DeterministicShuffle"/> (derived
        /// from the captured set), one or greater otherwise.
        /// </summary>
        public int SelectionCount { get; }

        /// <summary>
        /// The declared selection method.
        /// </summary>
        public SelectionMethod Method { get; }

        /// <summary>
        /// The declared replacement behavior; consistent with <see cref="Method"/> by construction.
        /// </summary>
        public ReplacementBehavior Replacement { get; }

        /// <summary>
        /// The declared weighting-rule identity; non-null iff <see cref="Method"/> is weighted, else null.
        /// </summary>
        public string WeightingRule { get; }

        /// <summary>
        /// The declared tie-break-rule identity.
        /// </summary>
        public string TieBreakRule { get; }

        /// <summary>
        /// The declared disposition of the unconsumed suffix; non-null iff <see cref="Method"/> is random
        /// ordering, else null.
        /// </summary>
        public string UnconsumedSuffixDisposition { get; }

        /// <summary>
        /// The declared behavior when the captured population is insufficient.
        /// </summary>
        public InsufficientCandidateBehavior InsufficientCandidates { get; }

        /// <summary>
        /// The isolated decision context whose selection purpose must equal <see cref="SelectionPurpose"/>.
        /// </summary>
        public DecisionContext Context { get; }

        public DecisionRequest(
            string selectionIdentity,
            string selectionPurpose,
            string selectionBoundary,
            string candidateSource,
            string eligibilityRule,
            string candidateSnapshotTiming,
            string candidateOrderingRule,
            int selectionCount,
            SelectionMethod method,
            ReplacementBehavior replacement,
            string weightingRule,
            string tieBreakRule,
            string unconsumedSuffixDisposition,
            InsufficientCandidateBehavior insufficientCandidates,
            DecisionContext context
        )
        {
            if (context == null)
                throw new ArgumentException("A decision request requires a context.", nameof(context));

            RequireNonEmpty(selectionIdentity, nameof(selectionIdentity));
            RequireNonEmpty(selectionPurpose, nameof(selectionPurpose));
            RequireNonEmpty(selectionBoundary, nameof(selectionBoundary));
            RequireNonEmpty(candidateSource, nameof(candidateSource));
            RequireNonEmpty(eligibilityRule, nameof(eligibilityRule));
            RequireNonEmpty(candidateSnapshotTiming, nameof(candidateSnapshotTiming));
            RequireNonEmpty(candidateOrderingRule, nameof(candidateOrderingRule));
            RequireNonEmpty(tieBreakRule, nameof(tieBreakRule));

            if (!SelectionPurposes.All.Contains(selectionPurpose))
            {
                throw new ArgumentException(
                    "The selection purpose is not a recognized purpose.",
                    nameof(selectionPurpose)
                );
            }

            if (!string.Equals(selectionPurpose, context.Components.SelectionPurpose, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The selection purpose must equal the context's selection purpose.",
                    nameof(selectionPurpose)
                );
            }

            if (method == SelectionMethod.DeterministicShuffle)
            {
                if (selectionCount != 0)
                {
                    throw new ArgumentException(
                        "A deterministic shuffle derives its count from the set; declare zero.",
                        nameof(selectionCount)
                    );
                }
            }
            else if (selectionCount < 1)
            {
                {
                    throw new ArgumentException(
                        "The selection count must be one or greater for this method.",
                        nameof(selectionCount)
                    );
                }
            }

            RequireReplacementConsistency(method, replacement);

            if ((weightingRule != null) != (method == SelectionMethod.WeightedSelection))
            {
                throw new ArgumentException(
                    "A weighting rule is present exactly for weighted selection.",
                    nameof(weightingRule)
                );
            }

            if ((unconsumedSuffixDisposition != null) != (method == SelectionMethod.RandomOrderingOfCapturedFiniteSet))
            {
                throw new ArgumentException(
                    "An unconsumed-suffix disposition is present exactly for random ordering.",
                    nameof(unconsumedSuffixDisposition)
                );
            }

            SelectionIdentity = selectionIdentity;
            SelectionPurpose = selectionPurpose;
            SelectionBoundary = selectionBoundary;
            CandidateSource = candidateSource;
            EligibilityRule = eligibilityRule;
            CandidateSnapshotTiming = candidateSnapshotTiming;
            CandidateOrderingRule = candidateOrderingRule;
            SelectionCount = selectionCount;
            Method = method;
            Replacement = replacement;
            WeightingRule = weightingRule;
            TieBreakRule = tieBreakRule;
            UnconsumedSuffixDisposition = unconsumedSuffixDisposition;
            InsufficientCandidates = insufficientCandidates;
            Context = context;
        }

        /// <summary>
        /// Rejects a null or empty required declaration string.
        /// </summary>
        /// <param name="value">The declaration string.</param>
        /// <param name="parameterName">The parameter name for the thrown exception.</param>
        private static void RequireNonEmpty(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(
                    "A required declaration field must be non-empty.",
                    parameterName
                );
            }
        }

        /// <summary>
        /// Rejects a replacement behavior inconsistent with the selection method.
        /// </summary>
        /// <param name="method">The declared selection method.</param>
        /// <param name="replacement">The declared replacement behavior.</param>
        private static void RequireReplacementConsistency(SelectionMethod method, ReplacementBehavior replacement)
        {
            switch (method)
            {
                case SelectionMethod.UniformSelectionWithoutReplacement:
                    if (replacement != ReplacementBehavior.RemovedForThisDecision && replacement != ReplacementBehavior.RemovedFromLongerLivedPopulation)
                    {
                        throw new ArgumentException(
                            "Uniform selection without replacement requires a removal behavior.",
                            nameof(replacement)
                        );
                    }
                    break;
                
                case SelectionMethod.UniformSelectionWithReplacement:
                case SelectionMethod.DeterministicShuffle:
                case SelectionMethod.RandomOrderingOfCapturedFiniteSet:
                    if (replacement != ReplacementBehavior.RemainsEligible)
                    {
                        throw new ArgumentException(
                            "This method requires the remains-eligible replacement behavior.",
                            nameof(replacement)
                        );
                    }
                    break;
            }
        }
    }
}