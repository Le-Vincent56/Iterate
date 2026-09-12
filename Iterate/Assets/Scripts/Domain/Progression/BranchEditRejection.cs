namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed reason a Branch inclusion or exclusion was rejected. Over-capacity is deliberately not
    /// here: an over-capacity draft is a legal, representable state the player edits their way out of,
    /// never an edit that fails.
    /// </summary>
    public enum BranchEditRejection
    {
        /// <summary>
        /// No rejection; the edit was applied.
        /// </summary>
        None,

        /// <summary>
        /// The instance is not in the Repository snapshot this configuration was opened against.
        /// </summary>
        NotARepositoryItem,

        /// <summary>
        /// The instance is held but its kind is not eligible for a Branch. No producer in this slice:
        /// every Repository item is an Instruction, Structure or Directive, and all three are eligible.
        /// </summary>
        NotEligibleKind,

        /// <summary>
        /// The instance satisfies a Required content rule and cannot be excluded.
        /// </summary>
        RequiredCannotBeExcluded,

        /// <summary>
        /// The instance's content is Quarantined for this Process and cannot be added.
        /// </summary>
        QuarantinedCannotBeIncluded,

        /// <summary>
        /// No configuration is open, so there is no draft to edit.
        /// </summary>
        NoConfigurationInProgress
    }
}