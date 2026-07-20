namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// How the current Build arrangement relates to what was last compiled: the CAB-EVT-643 classification,
    /// computed before cost and never changed by cost effects.
    /// </summary>
    public enum CompilationClassification
    {
        /// <summary>
        /// No prior compilation exists; this is the initial build.
        /// </summary>
        Initial,

        /// <summary>
        /// The current arrangement is equivalent to the compiled arrangement.
        /// </summary>
        Unchanged,

        /// <summary>
        /// The current arrangement differs from the compiled arrangement only by free-only actions.
        /// </summary>
        FreeOnlyChanged,

        /// <summary>
        /// The current arrangement includes ordinary edits over the compiled arrangement.
        /// </summary>
        OrdinaryEdited
    }
}