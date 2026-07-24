using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The running per-execution safety tallies read off the scheduler's own structures: the
    /// source-execution unit count, the effect-reaction count, the transformations applied to the
    /// current pending operation with a retained high-water, and the added-execution counters — the
    /// lineage-depth high-water read from proposed frame depths and the per-root-activation
    /// descendant tallies. Depth is never an independent counter that could drift from the frame
    /// stack; the preflights take the proposed depth the stack implies.
    /// </summary>
    public sealed class ExecutionSafetyTallies
    {
        /// <summary>
        /// The descendant counts per original canonical activation.
        /// </summary>
        private readonly Dictionary<RuntimeUnitID, int> _descendantsPerRoot = new Dictionary<RuntimeUnitID, int>();

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
        /// The deepest added-execution lineage depth recorded this execution.
        /// </summary>
        public int LineageDepthHighWater { get; private set; }

        /// <summary>
        /// The total added descendants created this execution across all root activations.
        /// </summary>
        public int AddedDescendants { get; private set; }

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
        /// Reports whether creating one more descendant is permitted: true while the proposed depth
        /// is at or below the lineage-depth ceiling and the root activation's descendant count is
        /// below the per-activation ceiling. Reaching a ceiling is permitted; the occurrence that
        /// would exceed it is not.
        /// </summary>
        /// <param name="proposedDepth">The descendant's proposed added-execution depth.</param>
        /// <param name="rootActivation">The original canonical activation the branch descends from.</param>
        /// <returns>True when the descendant may be created; false when either ceiling blocks it.</returns>
        public bool PreflightDescendant(int proposedDepth, RuntimeUnitID rootActivation)
        {
            if (proposedDepth > SafetyCeilings.AddedExecutionLineageDepth)
                return false;

            _descendantsPerRoot.TryGetValue(rootActivation, out int count);
            return count < SafetyCeilings.AddedExecutionsPerActivation;
        }

        /// <summary>
        /// Records one created descendant: increments the root activation's tally and the total,
        /// and raises the lineage-depth high-water when the proposed depth exceeds it.
        /// </summary>
        /// <param name="proposedDepth">The descendant's proposed added-execution depth.</param>
        /// <param name="rootActivation">The original canonical activation the branch descends from.</param>
        /// <exception cref="InvalidOperationException">Thrown when either ceiling would be exceeded — the scheduler must preflight first.</exception>
        public void RecordDescendant(int proposedDepth, RuntimeUnitID rootActivation)
        {
            if (proposedDepth > SafetyCeilings.AddedExecutionLineageDepth)
                throw new InvalidOperationException("The added-execution lineage-depth ceiling has been reached; a deeper descendant would exceed it.");

            _descendantsPerRoot.TryGetValue(rootActivation, out int count);
            if (count >= SafetyCeilings.AddedExecutionsPerActivation)
                throw new InvalidOperationException("The per-activation added-execution ceiling has been reached; another descendant would exceed it.");

            _descendantsPerRoot[rootActivation] = count + 1;
            AddedDescendants++;
            if (proposedDepth > LineageDepthHighWater)
                LineageDepthHighWater = proposedDepth;
        }

        /// <summary>
        /// Projects the running tallies to a high-water <see cref="SafetyCounts"/>.
        /// </summary>
        /// <returns>The safety counts for this execution.</returns>
        public SafetyCounts ToCounts()
        {
            return new SafetyCounts(LineageDepthHighWater, AddedDescendants, SourceExecutionUnits, EffectReactions, TransformationHighWater);
        }
    }
}