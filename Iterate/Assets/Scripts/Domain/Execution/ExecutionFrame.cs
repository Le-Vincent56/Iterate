using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One causal branch on the scheduler's explicit frame stack: the open runtime unit, the branch's
    /// effect-origin lineage, and the request batch captured before resolution. The batch is empty and the
    /// lineage is the empty lineage until added executions arrive in a later Epic child; the stack
    /// structure ships now so safety depth and lineage read directly off it.
    /// </summary>
    public sealed class ExecutionFrame
    {
        /// <summary>
        /// The open runtime unit this frame represents.
        /// </summary>
        public RuntimeUnitID Unit { get; }

        /// <summary>
        /// The branch's effect-origin lineage; the empty lineage at this content.
        /// </summary>
        public EffectOriginLineage Lineage { get; }

        /// <summary>
        /// The request batch captured before resolution; empty at this content.
        /// </summary>
        public IReadOnlyList<string> CapturedBatch { get; }

        public ExecutionFrame(RuntimeUnitID unit, EffectOriginLineage lineage)
        {
            Unit = unit;
            Lineage = lineage ?? throw new ArgumentException("An execution frame requires a lineage.", nameof(lineage));
            CapturedBatch = new List<string>();
        }
    }
}