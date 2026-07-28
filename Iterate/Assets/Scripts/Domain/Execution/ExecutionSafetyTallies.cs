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
    /// The tallies are pure counters and never emit evidence. Two query shapes sit on top of them:
    /// the boolean preflights, for sites needing only yes or no, and the evaluators, which answer
    /// the same question with every simultaneously breached limit so a breach can record all of
    /// them. The evaluators return null on the clear path and allocate only on a real breach, which
    /// happens at most once per execution. Which limits currently sit at their ceilings is a
    /// separate constant-time query; remembering which of those were reached for the first time
    /// belongs to the caller, not here.
    /// </summary>
    public sealed class ExecutionSafetyTallies
    {
        /// <summary>
        /// The descendant counts per original canonical activation.
        /// </summary>
        private readonly Dictionary<RuntimeUnitID, int> _descendantsPerRoot = new Dictionary<RuntimeUnitID, int>();

        /// <summary>
        /// The number of root activations whose descendant count has reached its ceiling, kept as a
        /// running count so the at-ceiling query stays constant-time and allocation-free.
        /// </summary>
        private int _rootsAtDescendantCeiling;

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
        /// The registry rows whose counts currently sit at their ceilings, in registry order.
        /// Reaching a ceiling is permitted, so this reports a legal state, not a breach. The
        /// per-activation row is set while any one root activation has reached its ceiling. The
        /// transformation row follows the live per-operation count and therefore clears when a new
        /// pending operation begins; a caller wanting first-contact-only behaviour keeps that memory
        /// itself.
        /// </summary>
        public SafetyLimitFlags LimitsAtCeiling
        {
            get
            {
                SafetyLimitFlags flags = SafetyLimitFlags.None;

                if (LineageDepthHighWater >= SafetyCeilings.AddedExecutionLineageDepth)
                    flags |= SafetyLimitFlags.AddedExecutionLineageDepth;

                if (_rootsAtDescendantCeiling > 0)
                    flags |= SafetyLimitFlags.AddedExecutionsPerActivation;

                if (SourceExecutionUnits >= SafetyCeilings.SourceExecutionUnitsPerExecution)
                    flags |= SafetyLimitFlags.SourceExecutionUnits;

                if (EffectReactions >= SafetyCeilings.EffectReactionsPerExecution)
                    flags |= SafetyLimitFlags.EffectReactions;

                if (TransformationsOnPendingOperation >= SafetyCeilings.TransformationsPerPendingOperation)
                    flags |= SafetyLimitFlags.TransformationsOnPendingOperation;

                return flags;
            }
        }

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
        /// Evaluates opening one more source-execution unit against every applicable ceiling.
        /// </summary>
        /// <returns>Null when the unit may be opened; otherwise every breached limit in registry order.</returns>
        public IReadOnlyList<BreachedLimit> EvaluateUnitOpening()
        {
            if (SourceExecutionUnits < SafetyCeilings.SourceExecutionUnitsPerExecution)
                return null;

            List<BreachedLimit> breached = new List<BreachedLimit>(1);
            breached.Add(new BreachedLimit(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.SourceExecutionUnits),
                SafetyCeilings.SourceExecutionUnitsPerExecution,
                SourceExecutionUnits
            ));

            return breached;
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
        /// Evaluates resolving one more effect reaction against every applicable ceiling.
        /// </summary>
        /// <returns>Null when the reaction may resolve; otherwise every breached limit in registry order.</returns>
        public IReadOnlyList<BreachedLimit> EvaluateReaction()
        {
            if (EffectReactions < SafetyCeilings.EffectReactionsPerExecution)
                return null;

            List<BreachedLimit> breached = new List<BreachedLimit>(1);
            breached.Add(new BreachedLimit(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.EffectReactions),
                SafetyCeilings.EffectReactionsPerExecution,
                EffectReactions
            ));

            return breached;
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
        /// Evaluates one more transformation of the current pending operation against every
        /// applicable ceiling.
        /// </summary>
        /// <returns>Null when the transformation may apply; otherwise every breached limit in registry order.</returns>
        public IReadOnlyList<BreachedLimit> EvaluateTransformation()
        {
            if (TransformationsOnPendingOperation < SafetyCeilings.TransformationsPerPendingOperation)
                return null;

            List<BreachedLimit> breached = new List<BreachedLimit>(1);
            breached.Add(new BreachedLimit(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.TransformationsOnPendingOperation),
                SafetyCeilings.TransformationsPerPendingOperation,
                TransformationsOnPendingOperation));

            return breached;
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
        /// Evaluates creating one more descendant against every ceiling one such occurrence
        /// advances: its lineage depth, its root activation's descendant count, and the execution's
        /// source-execution-unit count, each judged independently so a single attempt records all
        /// of them. The reaction that created the request is counted when that reaction resolves,
        /// so it is not evaluated a second time here.
        /// </summary>
        /// <param name="proposedDepth">The descendant's proposed added-execution depth.</param>
        /// <param name="rootActivation">The original canonical activation the branch descends from.</param>
        /// <returns>Null when the descendant may be created; otherwise every breached limit in registry order.</returns>
        public IReadOnlyList<BreachedLimit> EvaluateDescendant(int proposedDepth, RuntimeUnitID rootActivation)
        {
            bool depthBreached = proposedDepth > SafetyCeilings.AddedExecutionLineageDepth;
            _descendantsPerRoot.TryGetValue(rootActivation, out int rootCount);
            bool rootBreached = rootCount >= SafetyCeilings.AddedExecutionsPerActivation;
            bool unitsBreached = SourceExecutionUnits >= SafetyCeilings.SourceExecutionUnitsPerExecution;

            if (!depthBreached && !rootBreached && !unitsBreached)
                return null;

            List<BreachedLimit> breached = new List<BreachedLimit>(3);

            if (depthBreached)
            {
                breached.Add(new BreachedLimit(
                    SafetyAbortSignal.LimitName(SafetyLimitFlags.AddedExecutionLineageDepth),
                    SafetyCeilings.AddedExecutionLineageDepth,
                    proposedDepth));
            }

            if (rootBreached)
            {
                breached.Add(new BreachedLimit(
                    SafetyAbortSignal.LimitName(SafetyLimitFlags.AddedExecutionsPerActivation),
                    SafetyCeilings.AddedExecutionsPerActivation,
                    rootCount));
            }

            if (unitsBreached)
            {
                breached.Add(new BreachedLimit(
                    SafetyAbortSignal.LimitName(SafetyLimitFlags.SourceExecutionUnits),
                    SafetyCeilings.SourceExecutionUnitsPerExecution,
                    SourceExecutionUnits));
            }

            return breached;
        }

        /// <summary>
        /// Records one created descendant: increments the root activation's tally and the total,
        /// raises the lineage-depth high-water when the proposed depth exceeds it, and notes the
        /// root activation reaching its ceiling so the at-ceiling query stays constant-time.
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
            if (count + 1 == SafetyCeilings.AddedExecutionsPerActivation)
                _rootsAtDescendantCeiling++;

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