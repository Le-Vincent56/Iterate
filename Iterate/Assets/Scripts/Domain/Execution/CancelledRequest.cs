using System;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One cancelled added-execution request paired with the reason the tracker composed for it, so
    /// the reason vocabulary lives with the state machine that decides it rather than at the
    /// scheduler's evidence-emission sites.
    /// </summary>
    /// <param name="Request">The cancelled request; non-null.</param>
    /// <param name="Reason">The cancellation reason; non-empty.</param>
    public sealed record CancelledRequest(AddedExecutionRequest Request, string Reason)
    {
        /// <summary>
        /// The cancelled request. Validated non-null at construction.
        /// </summary>
        public AddedExecutionRequest Request { get; } = RequireRequest(Request);

        /// <summary>
        /// The cancellation reason. Validated non-empty at construction.
        /// </summary>
        public string Reason { get; } = RequireReason(Reason);

        /// <summary>
        /// Validates that the cancelled request is present.
        /// </summary>
        /// <param name="request">The candidate request.</param>
        /// <returns>The request unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the request is null.</exception>
        private static AddedExecutionRequest RequireRequest(AddedExecutionRequest request)
        {
            if (request == null)
                throw new ArgumentException("A cancelled request requires its request.", nameof(request));

            return request;
        }

        /// <summary>
        /// Validates that the cancellation reason is present.
        /// </summary>
        /// <param name="reason">The candidate reason.</param>
        /// <returns>The reason unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the reason is null or empty.</exception>
        private static string RequireReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                throw new ArgumentException("A cancelled request requires a reason.", nameof(reason));

            return reason;
        }
    }
}