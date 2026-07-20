namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// Why a commit was blocked: none, insufficient Bytes for the final cost, or the base cost undefined
    /// beyond the third edited compilation.
    /// </summary>
    public enum CompilationBlockReason
    {
        /// <summary>
        /// No block; the commit succeeded.
        /// </summary>
        None,

        /// <summary>
        /// The final cost exceeds the available Bytes.
        /// </summary>
        InsufficientBytes,

        /// <summary>
        /// No base cost is defined for the edited-compilation index.
        /// </summary>
        BaseCostUndefined
    }
}