namespace Iterate.Application.Content
{
    /// <summary>
    /// One pool membership, recorded during the pools pass so cross-file rules can read it.
    /// </summary>
    /// <param name="PoolID">The pool's identity.</param>
    /// <param name="ContentID">The member's content identity.</param>
    public sealed record PendingPoolMember(string PoolID, string ContentID);
}