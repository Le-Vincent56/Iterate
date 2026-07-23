namespace Iterate.Domain.Execution
{
    /// <summary>
    /// How an interpreted effect participates at the engine's boundaries: transforming the pending
    /// primary operation, reacting to a result, or rescuing a skipped source execution.
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
        Rescue
    }
}