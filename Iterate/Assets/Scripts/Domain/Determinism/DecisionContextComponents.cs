using System;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The applicable components of a random-decision context, in canonical order.
    /// <see cref="SessionSeedIdentity"/> and <see cref="SelectionPurpose"/> are required and non-empty and
    /// the <see cref="OccurrenceOrdinal"/> is one or greater; every other identity is optional, where null
    /// means <em>absent</em>. An empty string is never a valid absent value — absence is null, so an empty
    /// string is a data defect and throws. The components are retained on the derived context so
    /// divergence evidence can name which component differed.
    /// </summary>
    public sealed record DecisionContextComponents
    {
        /// <summary>
        /// The Session-scoped seed identity (required, non-empty).
        /// </summary>
        public string SessionSeedIdentity { get; }

        /// <summary>
        /// The governing content revision, or null when not applicable.
        /// </summary>
        public string ContentRevision { get; }

        /// <summary>
        /// The governing ruleset revision, or null when not applicable.
        /// </summary>
        public string RulesetRevision { get; }

        /// <summary>
        /// The governing System identity, or null when not applicable.
        /// </summary>
        public string SystemIdentity { get; }

        /// <summary>
        /// The governing Process identity, or null when not applicable.
        /// </summary>
        public string ProcessIdentity { get; }

        /// <summary>
        /// The execution, transaction, or lifecycle scope identity, or null when not applicable.
        /// </summary>
        public string ScopeIdentity { get; }

        /// <summary>
        /// The causing event identity, or null when not applicable.
        /// </summary>
        public string CausingEventIdentity { get; }

        /// <summary>
        /// The random effect-origin identity, or null when not applicable.
        /// </summary>
        public string EffectOriginIdentity { get; }

        /// <summary>
        /// The selection-purpose identity (required, non-empty).
        /// </summary>
        public string SelectionPurpose { get; }

        /// <summary>
        /// The per-purpose, per-context occurrence ordinal; one for the first occurrence (CAB-EVT-836).
        /// </summary>
        public int OccurrenceOrdinal { get; }

        public DecisionContextComponents(
            string sessionSeedIdentity,
            string contentRevision,
            string rulesetRevision,
            string systemIdentity,
            string processIdentity,
            string scopeIdentity,
            string causingEventIdentity,
            string effectOriginIdentity,
            string selectionPurpose,
            int occurrenceOrdinal
        )
        {
            if (string.IsNullOrEmpty(sessionSeedIdentity))
            {
                throw new ArgumentException(
                    "A session seed identity is required and must be non-empty.",
                    nameof(sessionSeedIdentity)
                );
            }

            if (string.IsNullOrEmpty(selectionPurpose))
            {
                throw new ArgumentException(
                    "A selection purpose is required and must be non-empty.",
                    nameof(selectionPurpose)
                );
            }

            if (occurrenceOrdinal < 1)
            {
                throw new ArgumentException(
                    "The occurrence ordinal must be one or greater.",
                    nameof(occurrenceOrdinal)
                );
            }

            RequireAbsentOrNonEmpty(contentRevision, nameof(contentRevision));
            RequireAbsentOrNonEmpty(rulesetRevision, nameof(rulesetRevision));
            RequireAbsentOrNonEmpty(systemIdentity, nameof(systemIdentity));
            RequireAbsentOrNonEmpty(processIdentity, nameof(processIdentity));
            RequireAbsentOrNonEmpty(scopeIdentity, nameof(scopeIdentity));
            RequireAbsentOrNonEmpty(causingEventIdentity, nameof(causingEventIdentity));
            RequireAbsentOrNonEmpty(effectOriginIdentity, nameof(effectOriginIdentity));

            SessionSeedIdentity = sessionSeedIdentity;
            ContentRevision = contentRevision;
            RulesetRevision = rulesetRevision;
            SystemIdentity = systemIdentity;
            ProcessIdentity = processIdentity;
            ScopeIdentity = scopeIdentity;
            CausingEventIdentity = causingEventIdentity;
            EffectOriginIdentity = effectOriginIdentity;
            SelectionPurpose = selectionPurpose;
            OccurrenceOrdinal = occurrenceOrdinal;
        }

        /// <summary>
        /// Rejects an optional identity that is present but empty; absence must be null, never the empty
        /// string.
        /// </summary>
        /// <param name="value">The optional identity to check.</param>
        /// <param name="parameterName">The constructor parameter name for the thrown exception.</param>
        private static void RequireAbsentOrNonEmpty(string value, string parameterName)
        {
            if (value is { Length: 0 })
            {
                throw new ArgumentException(
                    "An optional identity is absent (null) or non-empty; the empty string is invalid.",
                    parameterName
                );
            }
        }
    }
}