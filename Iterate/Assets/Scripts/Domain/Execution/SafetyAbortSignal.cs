using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The unwind signal a safety breach throws and the scheduler's execution entry point alone
    /// catches. It never crosses the engine's public seam: the entry point converts it into an
    /// aborted frozen record, so no caller of the scheduler ever observes it. It is also the single
    /// owner of the breach-evidence vocabulary — the closed limit-name set and the
    /// attempted-occurrence identity format — so the breach sites hand it typed facts and no
    /// emission site composes an evidence string of its own. The attempted occurrence is never
    /// created, so its identity is composed from the facts that would have made it rather than read
    /// off a minted record.
    /// </summary>
    public sealed class SafetyAbortSignal : Exception
    {
        /// <summary>
        /// The rendered name of the added-execution lineage-depth ceiling.
        /// </summary>
        private const string LineageDepthName = "ADDED_EXECUTION_LINEAGE_DEPTH";

        /// <summary>
        /// The rendered name of the per-activation added-execution ceiling.
        /// </summary>
        private const string PerActivationName = "ADDED_EXECUTIONS_PER_ACTIVATION";

        /// <summary>
        /// The rendered name of the source-execution-unit ceiling.
        /// </summary>
        private const string SourceExecutionUnitsName = "SOURCE_EXECUTION_UNITS_PER_EXECUTION";

        /// <summary>
        /// The rendered name of the effect-reaction ceiling.
        /// </summary>
        private const string EffectReactionsName = "EFFECT_REACTIONS_PER_EXECUTION";

        /// <summary>
        /// The rendered name of the per-pending-operation transformation ceiling.
        /// </summary>
        private const string TransformationsName = "TRANSFORMATIONS_PER_PENDING_OPERATION";

        /// <summary>
        /// The separator between an attempted occurrence's family and its subtype.
        /// </summary>
        private const string SubtypeSeparator = "/";

        /// <summary>
        /// The separator introducing an attempted occurrence's locating detail.
        /// </summary>
        private const string DetailSeparator = "@";

        /// <summary>
        /// The prefix of the signal's diagnostic message.
        /// </summary>
        private const string MessagePrefix = "Safety abort — the attempted occurrence would exceed a ceiling: ";

        /// <summary>
        /// The composed identity of the over-limit occurrence that was never created; never empty.
        /// </summary>
        public string OverLimitOccurrenceIdentity { get; }

        /// <summary>
        /// The unit whose activity breached the limit. The default identity means the breach
        /// occurred with no unit open.
        /// </summary>
        public RuntimeUnitID AffectedUnit { get; }

        /// <summary>
        /// Every simultaneously breached limit in registry order; never null, never empty, and
        /// carrying no hidden priority.
        /// </summary>
        public IReadOnlyList<BreachedLimit> BreachedLimits { get; }

        /// <summary>
        /// The diagnostic message, naming the attempted occurrence that was refused.
        /// </summary>
        public override string Message
        {
            get { return MessagePrefix + OverLimitOccurrenceIdentity; }
        }

        public SafetyAbortSignal(
            string attemptedFamily,
            string attemptedSubtype,
            string attemptedDetail,
            RuntimeUnitID affectedUnit,
            IReadOnlyList<BreachedLimit> breachedLimits
        )
        {
            if (breachedLimits == null || breachedLimits.Count == 0)
                throw new ArgumentException("A safety abort requires at least one breached limit.", nameof(breachedLimits));

            OverLimitOccurrenceIdentity = ComposeOccurrenceIdentity(attemptedFamily, attemptedSubtype, attemptedDetail);
            AffectedUnit = affectedUnit;
            BreachedLimits = breachedLimits;
        }

        /// <summary>
        /// Renders one registry row's closed name. These are engine-side identifiers naming the
        /// safety-count registry's rows; canon declares no limit-name token vocabulary.
        /// </summary>
        /// <param name="limit">Exactly one registry row.</param>
        /// <returns>The row's rendered name.</returns>
        /// <exception cref="ArgumentException">Thrown when the value is not exactly one registry row.</exception>
        public static string LimitName(SafetyLimitFlags limit)
        {
            switch (limit)
            {
                case SafetyLimitFlags.AddedExecutionLineageDepth:
                    return LineageDepthName;
                case SafetyLimitFlags.AddedExecutionsPerActivation:
                    return PerActivationName;
                case SafetyLimitFlags.SourceExecutionUnits:
                    return SourceExecutionUnitsName;
                case SafetyLimitFlags.EffectReactions:
                    return EffectReactionsName;
                case SafetyLimitFlags.TransformationsOnPendingOperation:
                    return TransformationsName;
                default:
                    throw new ArgumentException("A breached-limit name requires exactly one registry row.", nameof(limit));
            }
        }

        /// <summary>
        /// Composes the identity of an occurrence that was refused before creation, from the event
        /// family and subtype it would have carried plus an optional locating detail. The detail is
        /// omitted entirely when absent, so the format stays one shape.
        /// </summary>
        /// <param name="family">The event family the occurrence would have carried.</param>
        /// <param name="subtype">The event subtype the occurrence would have carried.</param>
        /// <param name="detail">The locating detail, or null when none applies.</param>
        /// <returns>The composed identity.</returns>
        /// <exception cref="ArgumentException">Thrown when the family or subtype is missing.</exception>
        public static string ComposeOccurrenceIdentity(string family, string subtype, string detail)
        {
            if (string.IsNullOrEmpty(family))
                throw new ArgumentException("An attempted-occurrence identity requires an event family.", nameof(family));

            if (string.IsNullOrEmpty(subtype))
                throw new ArgumentException("An attempted-occurrence identity requires an event subtype.", nameof(subtype));

            if (string.IsNullOrEmpty(detail))
                return family + SubtypeSeparator + subtype;

            return family + SubtypeSeparator + subtype + DetailSeparator + detail;
        }
    }
}