using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The per-execution pending added-execution state machine: a request established at a
    /// Condition's success waits for its locked host's unit to close, then matures into that unit's
    /// immediate-descendant batch when the closure succeeded and cancels when it did not. Requests
    /// still pending when traversal ends cancel too. The tracker owns both cancellation reason
    /// strings, so the evidence vocabulary is decided and tested here rather than at the scheduler's
    /// emission sites; a cancelled request's consumed allowance always stays consumed.
    /// </summary>
    public sealed class PendingAddedExecutionTracker
    {
        /// <summary>
        /// The reason prefix for a locked host that closed unsuccessfully.
        /// </summary>
        private const string LockedHostUnsuccessful = "LOCKED_HOST_UNSUCCESSFUL:";

        /// <summary>
        /// The reason for a request still pending when the execution closed.
        /// </summary>
        private const string ExecutionClosedPending = "EXECUTION_CLOSED_PENDING";

        /// <summary>
        /// The established requests awaiting their locked hosts, in establishment order.
        /// </summary>
        private readonly List<AddedExecutionRequest> _pending = new List<AddedExecutionRequest>();

        /// <summary>
        /// Establishes one committed request as pending on its locked host.
        /// </summary>
        /// <param name="request">The committed request; its parent unit must still be absent.</param>
        /// <exception cref="ArgumentException">Thrown when the request is null or already parented.</exception>
        public void Establish(AddedExecutionRequest request)
        {
            if (request == null)
                throw new ArgumentException("Establishing requires a request.", nameof(request));

            if (request.ParentUnit != null)
                throw new ArgumentException("A pending request has no parent unit until its locked host closes.", nameof(request));

            _pending.Add(request);
        }

        /// <summary>
        /// Offers a closed host to the tracker: a request locked to it matures on a successful
        /// closure and cancels on any other, and either way leaves the tracker. A host with no
        /// pending request resolves to none.
        /// </summary>
        /// <param name="hostInstance">The closed unit's host instance.</param>
        /// <param name="finalDisposition">The closed unit's final disposition.</param>
        /// <param name="matured">The matured request, or null.</param>
        /// <param name="cancelled">The cancelled request with its reason, or null.</param>
        /// <returns>The resolution the offer produced.</returns>
        public PendingResolution TryClose(
            InstanceID hostInstance,
            EventDisposition finalDisposition,
            out AddedExecutionRequest matured,
            out CancelledRequest cancelled
        )
        {
            matured = null;
            cancelled = null;

            for (int i = 0; i < _pending.Count; i++)
            {
                AddedExecutionRequest request = _pending[i];
                if (request.LockedSlot.Instruction.InstanceID != hostInstance)
                    continue;

                _pending.RemoveAt(i);
                if (finalDisposition == EventDisposition.Resolved || finalDisposition == EventDisposition.Rescued)
                {
                    matured = request;
                    return PendingResolution.Matured;
                }

                cancelled = new CancelledRequest(request, LockedHostUnsuccessful + DispositionToken(finalDisposition));
                return PendingResolution.Cancelled;
            }

            return PendingResolution.None;
        }

        /// <summary>
        /// Removes and returns every still-pending request, each paired with the execution-close
        /// cancellation reason.
        /// </summary>
        /// <returns>The cancelled requests in establishment order; empty when none remain.</returns>
        public IReadOnlyList<CancelledRequest> DrainPending()
        {
            if (_pending.Count == 0)
                return Array.Empty<CancelledRequest>();

            List<CancelledRequest> drained = new List<CancelledRequest>(_pending.Count);
            for (int i = 0; i < _pending.Count; i++)
            {
                drained.Add(new CancelledRequest(_pending[i], ExecutionClosedPending));
            }

            _pending.Clear();
            return drained;
        }

        /// <summary>
        /// Returns the canonical disposition token a cancellation reason names.
        /// </summary>
        /// <param name="disposition">The closed unit's final disposition.</param>
        /// <returns>The disposition token.</returns>
        /// <exception cref="ArgumentException">Thrown when the disposition is not a known member.</exception>
        private static string DispositionToken(EventDisposition disposition)
        {
            switch (disposition)
            {
                case EventDisposition.Resolved:
                    return "RESOLVED";

                case EventDisposition.FailedToQualify:
                    return "FAILED_TO_QUALIFY";

                case EventDisposition.Skipped:
                    return "SKIPPED";

                case EventDisposition.Prevented:
                    return "PREVENTED";

                case EventDisposition.Cancelled:
                    return "CANCELLED";

                case EventDisposition.Rescued:
                    return "RESCUED";

                default:
                    throw new ArgumentException($"Unknown disposition {disposition}.", nameof(disposition));
            }
        }
    }
}