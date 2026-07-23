namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The canon-verbatim selected-host effect set, bound over catalog keys: STANDARD LIBRARY chooses
    /// one Instruction instance at commitment and applies its behavior to later executions of that
    /// same instance for the execution, never migrating to a later equivalent Instruction. The set is
    /// never generalized by inference — an effect whose wording describes one qualifying event keeps
    /// first-successful-event semantics; any addition requires its own canon citation.
    /// </summary>
    public static class SelectedHostEffects
    {
        /// <summary>
        /// The STANDARD LIBRARY catalog key — the one shipped selected-host effect.
        /// </summary>
        private const string StandardLibrary = "WB-DEP-001";

        /// <summary>
        /// Returns whether the definition uses selected-host semantics.
        /// </summary>
        /// <param name="definitionID">The definition's surrogate-key identity.</param>
        /// <returns>True exactly for the STANDARD LIBRARY catalog key.</returns>
        public static bool IsSelectedHost(string definitionID)
        {
            return definitionID == StandardLibrary;
        }
    }
}