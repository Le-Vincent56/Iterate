namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Leaves the open shop: pins clear, completed purchases stay, and the Session moves to the next
    /// step.
    /// </summary>
    public sealed record LeaveShop : ISessionCommand;
}