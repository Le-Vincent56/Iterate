using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One causal branch on the scheduler's explicit frame stack: the open runtime unit, the
    /// branch's effect-origin lineage, its added-execution depth, the original canonical activation
    /// the branch descends from, and the typed request batch captured before resolution. Canonical
    /// units are self-rooted at depth zero with the empty lineage; descendant units carry their
    /// request's lineage and depth under the parent's root.
    /// </summary>
    public sealed class ExecutionFrame
    {
        /// <summary>
        /// The requests captured on this branch, in capture order.
        /// </summary>
        private readonly List<AddedExecutionRequest> _capturedBatch;

        /// <summary>
        /// The open runtime unit this frame represents.
        /// </summary>
        public RuntimeUnitID Unit { get; }

        /// <summary>
        /// The branch's effect-origin lineage; the empty lineage on canonical branches.
        /// </summary>
        public EffectOriginLineage Lineage { get; }

        /// <summary>
        /// The branch's added-execution depth; zero on canonical branches.
        /// </summary>
        public int AddedExecutionDepth { get; }

        /// <summary>
        /// The original canonical activation this branch descends from; the unit itself on
        /// canonical branches.
        /// </summary>
        public RuntimeUnitID RootActivation { get; }

        /// <summary>
        /// The request batch captured before resolution, in capture order.
        /// </summary>
        public IReadOnlyList<AddedExecutionRequest> CapturedBatch => _capturedBatch;

        public ExecutionFrame(
            RuntimeUnitID unit,
            EffectOriginLineage lineage,
            int addedExecutionDepth,
            RuntimeUnitID rootActivation
        )
        {
            if (addedExecutionDepth < 0)
                throw new ArgumentException("An execution frame's added-execution depth may not be negative.", nameof(addedExecutionDepth));

            Unit = unit;
            Lineage = lineage ?? throw new ArgumentException("An execution frame requires a lineage.", nameof(lineage));
            AddedExecutionDepth = addedExecutionDepth;
            RootActivation = rootActivation;
            _capturedBatch = new List<AddedExecutionRequest>();
        }

        /// <summary>
        /// Appends one committed request to this branch's captured batch.
        /// </summary>
        /// <param name="request">The committed request.</param>
        /// <exception cref="ArgumentException">Thrown when the request is null.</exception>
        public void Capture(AddedExecutionRequest request)
        {
            if (request == null)
                throw new ArgumentException("A captured request may not be null.", nameof(request));

            _capturedBatch.Add(request);
        }
    }
}