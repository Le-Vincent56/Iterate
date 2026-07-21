using System;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The running per-execution safety tallies read off the scheduler's own structures. At this content
    /// only the source-execution unit count grows; the other four counts stay structurally zero until
    /// later Epic children produce them. Lineage depth will be derived from the frame stack's
    /// added-execution depth when that child lands — these tallies never grow an independent depth counter
    /// that could drift from the stack.
    /// </summary>
    public sealed class ExecutionSafetyTallies
    {
        /// <summary>
        /// The number of source-execution units opened so far.
        /// </summary>
        public int SourceExecutionUnits { get; private set; }

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
        /// Projects the running tallies to a high-water <see cref="SafetyCounts"/>. Lineage depth, added
        /// descendants, effect reactions, and operation transformations are structurally zero at this
        /// content.
        /// </summary>
        /// <returns>The safety counts for this execution.</returns>
        public SafetyCounts ToCounts()
        {
            return new SafetyCounts(0, 0, SourceExecutionUnits, 0, 0);
        }
    }
}