namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// One Command-typed source edit: it validates itself against an arrangement and buffer, and on
    /// success produces a new arrangement (plus any archived instances). It declares whether it is
    /// free-only. It carries no queuing, history, or undo.
    /// </summary>
    public interface ISourceEdit
    {
        /// <summary>
        /// Whether this edit is free-only (does not count as an ordinary edit for classification).
        /// </summary>
        bool IsFreeOnly { get; }

        /// <summary>
        /// Validates and, on success, applies the edit, taking from or accepting into the buffer as
        /// required. The buffer is touched only after every check passes.
        /// </summary>
        /// <param name="current">The arrangement to validate against.</param>
        /// <param name="buffer">The buffer seam to peek, take from, or accept into.</param>
        /// <param name="outcome">The produced outcome on success; null when rejected.</param>
        /// <returns>Success, or a typed rejection.</returns>
        BuildOperationResult TryApply(SourceArrangement current, IBuildBuffer buffer, out SourceEditOutcome outcome);
    }
}