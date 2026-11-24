namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The outcome of beginning a Session.
    /// </summary>
    /// <param name="Succeeded">Whether the Session began.</param>
    /// <param name="Rejection">Why not; None on success.</param>
    public sealed record SessionBeginResult(bool Succeeded, SessionBeginRejection Rejection);
}