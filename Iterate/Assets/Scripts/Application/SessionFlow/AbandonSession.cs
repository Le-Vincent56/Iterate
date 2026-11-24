namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Abandons the Session from any active stage.
    /// </summary>
    public sealed record AbandonSession : ISessionCommand;
}