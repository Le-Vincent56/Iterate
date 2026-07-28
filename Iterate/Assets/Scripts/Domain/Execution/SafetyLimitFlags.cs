using System;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The five safety-count registry rows as single-bit flags in registry order. The flags are the
    /// closed identity vocabulary for a safety limit; their rendered names live with
    /// <see cref="SafetyAbortSignal"/>, the single owner of breach-evidence strings. A combination
    /// is meaningful only as a set of limits — several ceilings can be contacted or breached at
    /// once — so no combined member is declared.
    /// </summary>
    [Flags]
    public enum SafetyLimitFlags
    {
        /// <summary>
        /// No limit.
        /// </summary>
        None = 0,

        /// <summary>
        /// The added-execution lineage-depth ceiling.
        /// </summary>
        AddedExecutionLineageDepth = 1,

        /// <summary>
        /// The ceiling on added source executions descended from one original source activation.
        /// </summary>
        AddedExecutionsPerActivation = 2,

        /// <summary>
        /// The ceiling on total source-execution units in one execution.
        /// </summary>
        SourceExecutionUnits = 4,

        /// <summary>
        /// The ceiling on resolved effect reactions in one execution.
        /// </summary>
        EffectReactions = 8,

        /// <summary>
        /// The ceiling on transformations applied to one pending operation.
        /// </summary>
        TransformationsOnPendingOperation = 16
    }
}