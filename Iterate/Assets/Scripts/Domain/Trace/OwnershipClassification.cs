namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The source-ownership classification every source-origin record identifies explicitly:
    /// player-owned source objects versus immutable Core-owned source lines.
    /// </summary>
    public enum OwnershipClassification
    {
        /// <summary>
        /// The host is a player-owned source object.
        /// </summary>
        PlayerOwned,

        /// <summary>
        /// The host is an immutable Core-owned source line.
        /// </summary>
        CoreOwned
    }
}