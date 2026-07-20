using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The outcome of a commit attempt: committed with the produced source, or blocked with a reason. The
    /// breakdown and available Bytes ride both outcomes so the interface can explain a block (UX-RUN-013).
    /// </summary>
    /// <param name="Committed">Whether the commit succeeded.</param>
    /// <param name="BlockReason">The block reason; None when committed.</param>
    /// <param name="Source">The produced compiled source; null when blocked.</param>
    /// <param name="Breakdown">The cost breakdown, present in both outcomes.</param>
    /// <param name="AvailableBytes">The Bytes available at the attempt.</param>
    public sealed record CompilationAttempt(
        bool Committed,
        CompilationBlockReason BlockReason,
        CompiledSource Source,
        CompilationCostBreakdown Breakdown,
        ByteAmount AvailableBytes
    )
    {
        /// <summary>
        /// Creates a committed attempt.
        /// </summary>
        /// <param name="source">The produced compiled source.</param>
        /// <param name="breakdown">The cost breakdown.</param>
        /// <param name="availableBytes">The Bytes available.</param>
        /// <returns>A committed attempt.</returns>
        public static CompilationAttempt Success(CompiledSource source, CompilationCostBreakdown breakdown, ByteAmount availableBytes)
        {
            return new CompilationAttempt(true, CompilationBlockReason.None, source, breakdown, availableBytes);
        }

        /// <summary>
        /// Creates a blocked attempt.
        /// </summary>
        /// <param name="reason">The block reason.</param>
        /// <param name="breakdown">The cost breakdown.</param>
        /// <param name="availableBytes">The Bytes available.</param>
        /// <returns>A blocked attempt.</returns>
        public static CompilationAttempt Blocked(CompilationBlockReason reason, CompilationCostBreakdown breakdown, ByteAmount availableBytes)
        {
            return new CompilationAttempt(false, reason, null, breakdown, availableBytes);
        }
    }
}