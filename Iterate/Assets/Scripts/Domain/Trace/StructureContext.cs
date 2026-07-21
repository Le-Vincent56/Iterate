using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The Structure ancestry an event or unit executes within: the root-first chain of enclosing
    /// Structure instances, the entry identity, and — when applicable — the Repeat-iteration and
    /// Condition-evaluation identities. Iteration and Condition identities are null when not applicable;
    /// an empty string is rejected as a defect rather than a not-applicable marker. Equality is
    /// structural over the ancestry.
    /// </summary>
    public sealed record StructureContext
    {
        /// <summary>
        /// The enclosing Structure instances in root-first order; never null, never empty.
        /// </summary>
        public IReadOnlyList<InstanceID> StructureAncestry { get; }

        /// <summary>
        /// The identity of the Structure entry this context belongs to; never empty.
        /// </summary>
        public string StructureEntryIdentity { get; }

        /// <summary>
        /// The Repeat-iteration identity, or null when the context is not inside a Repeat iteration.
        /// </summary>
        public string RepeatIterationIdentity { get; }

        /// <summary>
        /// The Condition-evaluation identity, or null when the context is not a Condition evaluation.
        /// </summary>
        public string ConditionEvaluationIdentity { get; }

        public StructureContext(
            IReadOnlyList<InstanceID> structureAncestry,
            string structureEntryIdentity,
            string repeatIterationIdentity,
            string conditionEvaluationIdentity
        )
        {
            if (structureAncestry == null || structureAncestry.Count == 0)
                throw new ArgumentException("A StructureContext requires a non-empty Structure ancestry.", nameof(structureAncestry));
            
            if (string.IsNullOrEmpty(structureEntryIdentity))
                throw new ArgumentException("A StructureContext requires a Structure entry identity.", nameof(structureEntryIdentity));
            
            if (repeatIterationIdentity != null && repeatIterationIdentity.Length == 0)
                throw new ArgumentException("A Repeat-iteration identity is null when not applicable; an empty string is invalid.", nameof(repeatIterationIdentity));
            
            if (conditionEvaluationIdentity != null && conditionEvaluationIdentity.Length == 0)
                throw new ArgumentException("A Condition-evaluation identity is null when not applicable; an empty string is invalid.", nameof(conditionEvaluationIdentity));

            StructureAncestry = structureAncestry;
            StructureEntryIdentity = structureEntryIdentity;
            RepeatIterationIdentity = repeatIterationIdentity;
            ConditionEvaluationIdentity = conditionEvaluationIdentity;
        }

        /// <summary>
        /// Structural value equality over the ancestry and the three identity fields.
        /// </summary>
        /// <param name="other">The context to compare against.</param>
        /// <returns>True when the ancestry and every identity field are equal.</returns>
        public bool Equals(StructureContext other)
        {
            if (other == null) return false;

            return string.Equals(StructureEntryIdentity, other.StructureEntryIdentity, StringComparison.Ordinal)
                && string.Equals(RepeatIterationIdentity, other.RepeatIterationIdentity, StringComparison.Ordinal)
                && string.Equals(ConditionEvaluationIdentity, other.ConditionEvaluationIdentity, StringComparison.Ordinal)
                && TraceEquality.ListEquals(StructureAncestry, other.StructureAncestry);
        }

        /// <summary>
        /// A hash consistent with structural equality, over the ancestry count and the entry identity.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + StructureAncestry.Count;
                hash = hash * 31 + StructureEntryIdentity.GetHashCode();
                return hash;
            }
        }
    }
}