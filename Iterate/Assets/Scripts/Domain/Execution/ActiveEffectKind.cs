namespace Iterate.Domain.Execution
{
    /// <summary>
    /// How an interpreted effect participates at the engine's boundaries: transforming the pending
    /// primary operation, reacting to a result, rescuing a skipped source execution, creating an
    /// added-execution request, or resolving at a named traversal boundary.
    /// </summary>
    public enum ActiveEffectKind
    {
        /// <summary>
        /// Transforms the pending primary operation at the modification band.
        /// </summary>
        Modification,

        /// <summary>
        /// Reacts to a resolved operation or quantity change at the immediate-reaction band.
        /// </summary>
        Reaction,

        /// <summary>
        /// Rescues a skipped source execution at the pre-operation band.
        /// </summary>
        Rescue,

        /// <summary>
        /// Creates one added-execution request from a qualifying occurrence.
        /// </summary>
        AddedExecution,

        /// <summary>
        /// Resolves a source-less quantity change at a named traversal boundary.
        /// </summary>
        Boundary
    }
}