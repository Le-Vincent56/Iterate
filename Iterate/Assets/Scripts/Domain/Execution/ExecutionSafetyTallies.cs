using System;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The running per-execution safety tallies read off the scheduler's own structures: the
    /// source-execution unit count, the effect-reaction count, and the transformations applied to the
    /// current pending operation with a retained high-water. Lineage depth will be derived from the
    /// frame stack's added-execution depth when that child lands — these tallies never grow an
    /// independent depth counter that could drift from the stack.
    /// </summary>
    public sealed class ExecutionSafetyTallies
    {
        /// <summary>
        /// The number of source-execution units opened so far.
        /// </summary>
        public int SourceExecutionUnits { get; private set; }

        /// <summary>
        /// The number of effect reactions resolved so far this execution.
        /// </summary>
        public int EffectReactions { get; private set; }

        /// <summary>
        /// The number of transformations applied to the current pending operation.
        /// </summary>
        public int TransformationsOnPendingOperation { get; private set; }

        /// <summary>
        /// The highest per-pending-operation transformation count reached this execution.
        /// </summary>
        public int TransformationHighWater { get; private set; }

        /// <summary>
        /// Reports whether opening one more source-execution unit is permitted: true while below the
        /// ceiling, false once it is reached. Reaching the ceiling is permitted; the occurrence that would
        /// exceed it is not.
        /// </summary>
        /// <returns>True when a further unit may be opened; false at the ceiling.</returns>
        public bool PreflightUnitOpening()
        {
            return SourceExecutionUnits < SafetyCeilings.SourceExecutionUnitsPerExecution;
        }

        /// <summary>
        /// Records that a source-execution unit was opened, incrementing the count.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the ceiling has already been reached — the scheduler must preflight first.</exception>
        public void RecordUnitOpened()
        {
            if (SourceExecutionUnits >= SafetyCeilings.SourceExecutionUnitsPerExecution)
                throw new InvalidOperationException("The source-execution unit ceiling has been reached; opening another would exceed it.");

            SourceExecutionUnits++;
        }

        /// <summary>
        /// Reports whether resolving one more effect reaction is permitted: true while below the
        /// ceiling, false once it is reached.
        /// </summary>
        /// <returns>True when a further reaction may resolve; false at the ceiling.</returns>
        public bool PreflightReaction()
        {
            return EffectReactions < SafetyCeilings.EffectReactionsPerExecution;
        }

        /// <summary>
        /// Records that an effect reaction resolved, incrementing the count.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the ceiling has already been reached — the scheduler must preflight first.</exception>
        public void RecordReaction()
        {
            if (EffectReactions >= SafetyCeilings.EffectReactionsPerExecution)
                throw new InvalidOperationException("The effect-reaction ceiling has been reached; resolving another would exceed it.");

            EffectReactions++;
        }

        /// <summary>
        /// Resets the per-operation transformation count for a newly established pending operation.
        /// The high-water is retained across operations.
        /// </summary>
        public void BeginPendingOperation()
        {
            TransformationsOnPendingOperation = 0;
        }

        /// <summary>
        /// Reports whether one more transformation of the current pending operation is permitted:
        /// true while below the ceiling, false once it is reached.
        /// </summary>
        /// <returns>True when a further transformation may apply; false at the ceiling.</returns>
        public bool PreflightTransformation()
        {
            return TransformationsOnPendingOperation < SafetyCeilings.TransformationsPerPendingOperation;
        }

        /// <summary>
        /// Records a transformation of the current pending operation, incrementing the per-operation
        /// count and raising the high-water.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the ceiling has already been reached — the scheduler must preflight first.</exception>
        public void RecordTransformation()
        {
            if (TransformationsOnPendingOperation >= SafetyCeilings.TransformationsPerPendingOperation)
                throw new InvalidOperationException("The per-operation transformation ceiling has been reached; applying another would exceed it.");

            TransformationsOnPendingOperation++;
            if (TransformationsOnPendingOperation > TransformationHighWater)
                TransformationHighWater = TransformationsOnPendingOperation;
        }

        /// <summary>
        /// Projects the running tallies to a high-water <see cref="SafetyCounts"/>. Lineage depth and
        /// added descendants are structurally zero at this content.
        /// </summary>
        /// <returns>The safety counts for this execution.</returns>
        public SafetyCounts ToCounts()
        {
            return new SafetyCounts(0, 0, SourceExecutionUnits, EffectReactions, TransformationHighWater);
        }
    }
}