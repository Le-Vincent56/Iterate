namespace Iterate.Application.Persistence
{
    /// <summary>
    /// How clearing the checkpoint ended. Clearing one that is already gone is reported distinctly from
    /// clearing one that was there, so a caller that cares can tell.
    /// </summary>
    public enum ClearOutcome
    {
        /// <summary>
        /// The checkpoint existed and was removed.
        /// </summary>
        Cleared,

        /// <summary>
        /// There was no checkpoint to remove.
        /// </summary>
        Absent,

        /// <summary>
        /// The checkpoint exists and could not be removed.
        /// </summary>
        Failed
    }
}