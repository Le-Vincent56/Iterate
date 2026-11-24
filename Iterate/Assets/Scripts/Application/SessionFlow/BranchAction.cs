namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// What a <see cref="ConfigureBranch"/> command does to the Active Branch being configured.
    /// </summary>
    public enum BranchAction
    {
        /// <summary>
        /// Marks an item eligible.
        /// </summary>
        Include,

        /// <summary>
        /// Marks an item ineligible.
        /// </summary>
        Exclude,

        /// <summary>
        /// Confirms the configuration and enters the Process; carries no item.
        /// </summary>
        Confirm,
    }
}