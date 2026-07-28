using System;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Diagnostics
{
    /// <summary>
    /// The typed result of one replay comparison: the status, exactly one matching detail branch, and
    /// the recomputed record when — and only when — the candidate inputs corresponded to the stored
    /// ones. An exact-reproduction-unavailable result withholds the recomputed record even though the
    /// engine did run, so a record computed under non-corresponding inputs cannot be substituted for the
    /// stored execution's evidence by any downstream consumer (CAB-EVT-770). Withholding is what makes
    /// that substitution structurally impossible rather than merely discouraged.
    /// </summary>
    public sealed record ReplayComparisonResult
    {
        /// <summary>
        /// The outcome of the comparison.
        /// </summary>
        public ReplayComparisonStatus Status { get; }

        /// <summary>
        /// The first header component that failed to correspond, non-null exactly when the status is
        /// <see cref="ReplayComparisonStatus.ExactReproductionUnavailable"/>.
        /// </summary>
        public string UnavailableComponent { get; }

        /// <summary>
        /// The identified first difference, non-null exactly when the status is
        /// <see cref="ReplayComparisonStatus.Diverged"/>.
        /// </summary>
        public ReplayDivergence Divergence { get; }

        /// <summary>
        /// The record produced by re-resolving the candidate request, non-null exactly when
        /// correspondence held, and withheld when it did not.
        /// </summary>
        public ExecutionRecord RecomputedRecord { get; }

        public ReplayComparisonResult(
            ReplayComparisonStatus status,
            string unavailableComponent,
            ReplayDivergence divergence,
            ExecutionRecord recomputedRecord
        )
        {
            switch (status)
            {
                case ReplayComparisonStatus.Match:
                    if (divergence != null)
                        throw new ArgumentException("A match carries no divergence.", nameof(divergence));

                    if (unavailableComponent != null)
                        throw new ArgumentException("A match carries no unavailable component.", nameof(unavailableComponent));

                    if (recomputedRecord == null)
                        throw new ArgumentException("A match requires the recomputed record.", nameof(recomputedRecord));

                    break;

                case ReplayComparisonStatus.Diverged:
                    if (divergence == null)
                        throw new ArgumentException("A divergence result requires the identified divergence.", nameof(divergence));

                    if (unavailableComponent != null)
                        throw new ArgumentException("A divergence result carries no unavailable component.", nameof(unavailableComponent));

                    if (recomputedRecord == null)
                        throw new ArgumentException("A divergence result requires the recomputed record.", nameof(recomputedRecord));

                    break;

                case ReplayComparisonStatus.ExactReproductionUnavailable:
                    if (string.IsNullOrEmpty(unavailableComponent))
                        throw new ArgumentException("An unavailable result requires the first non-corresponding component.", nameof(unavailableComponent));

                    if (divergence != null)
                        throw new ArgumentException("An unavailable result carries no divergence.", nameof(divergence));

                    if (recomputedRecord != null)
                        throw new ArgumentException("An unavailable result withholds the recomputed record.", nameof(recomputedRecord));

                    break;

                default:
                    throw new ArgumentException("A comparison result requires a defined status.", nameof(status));
            }

            Status = status;
            UnavailableComponent = unavailableComponent;
            Divergence = divergence;
            RecomputedRecord = recomputedRecord;
        }
    }
}