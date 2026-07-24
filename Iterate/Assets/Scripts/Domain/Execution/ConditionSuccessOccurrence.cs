using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One successful Condition evaluation offered to the effect engine at the post-unit band: the
    /// result event, the evaluation identity, the first occupied contained child with its position
    /// when one exists, the evaluation's Structure context, and the offering branch's lineage. A
    /// `TRUE` evaluation with no occupied child offers no legal host.
    /// </summary>
    /// <param name="ResultEvent">The Condition-true result event.</param>
    /// <param name="EvaluationIdentity">The evaluation's identity; never empty.</param>
    /// <param name="FirstOccupiedChild">The first occupied contained Instruction, or null when none.</param>
    /// <param name="FirstOccupiedChildPosition">That child's source position; present with the child.</param>
    /// <param name="EvaluationContext">The evaluation's Structure context; never null.</param>
    /// <param name="BranchLineage">The offering branch's effect-origin lineage; never null.</param>
    public sealed record ConditionSuccessOccurrence(
        TraceEventID ResultEvent,
        string EvaluationIdentity,
        InstanceID? FirstOccupiedChild,
        SourcePosition? FirstOccupiedChildPosition,
        StructureContext EvaluationContext,
        EffectOriginLineage BranchLineage
    )
    {
        /// <summary>
        /// The evaluation's identity. Validated non-empty at construction.
        /// </summary>
        public string EvaluationIdentity { get; } = RequireIdentity(EvaluationIdentity);

        /// <summary>
        /// That child's source position. Validated at construction: present exactly when the child is.
        /// </summary>
        public SourcePosition? FirstOccupiedChildPosition { get; } = RequireChildPairing(FirstOccupiedChild, FirstOccupiedChildPosition);

        /// <summary>
        /// The evaluation's Structure context. Validated non-null at construction.
        /// </summary>
        public StructureContext EvaluationContext { get; } = RequireContext(EvaluationContext);

        /// <summary>
        /// The offering branch's effect-origin lineage. Validated non-null at construction.
        /// </summary>
        public EffectOriginLineage BranchLineage { get; } = RequireLineage(BranchLineage);

        /// <summary>
        /// Validates that the evaluation identity is present.
        /// </summary>
        /// <param name="evaluationIdentity">The candidate identity.</param>
        /// <returns>The identity unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the identity is null or empty.</exception>
        private static string RequireIdentity(string evaluationIdentity)
        {
            if (string.IsNullOrEmpty(evaluationIdentity))
                throw new ArgumentException("A Condition-success occurrence requires an evaluation identity.", nameof(evaluationIdentity));

            return evaluationIdentity;
        }

        /// <summary>
        /// Validates that the child's position is present exactly when the child is.
        /// </summary>
        /// <param name="firstOccupiedChild">The candidate child instance.</param>
        /// <param name="firstOccupiedChildPosition">The candidate child position.</param>
        /// <returns>The position unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when child and position disagree.</exception>
        private static SourcePosition? RequireChildPairing(
            InstanceID? firstOccupiedChild,
            SourcePosition? firstOccupiedChildPosition
        )
        {
            if (firstOccupiedChild != null && firstOccupiedChildPosition == null)
                throw new ArgumentException("An occupied child requires its source position.", nameof(firstOccupiedChildPosition));

            if (firstOccupiedChild == null && firstOccupiedChildPosition != null)
                throw new ArgumentException("A child position requires its occupied child.", nameof(firstOccupiedChildPosition));

            return firstOccupiedChildPosition;
        }

        /// <summary>
        /// Validates that the evaluation context is present.
        /// </summary>
        /// <param name="evaluationContext">The candidate context.</param>
        /// <returns>The context unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the context is null.</exception>
        private static StructureContext RequireContext(StructureContext evaluationContext)
        {
            if (evaluationContext == null)
                throw new ArgumentException("A Condition-success occurrence requires its evaluation context.", nameof(evaluationContext));

            return evaluationContext;
        }

        /// <summary>
        /// Validates that the branch lineage is present.
        /// </summary>
        /// <param name="branchLineage">The candidate lineage.</param>
        /// <returns>The lineage unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the lineage is null.</exception>
        private static EffectOriginLineage RequireLineage(EffectOriginLineage branchLineage)
        {
            if (branchLineage == null)
                throw new ArgumentException("An occurrence requires a branch lineage.", nameof(branchLineage));

            return branchLineage;
        }
    }
}