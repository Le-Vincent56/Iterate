namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Discards the reviewed result and returns to Build for another execution.
    /// </summary>
    public sealed record Continue : ISessionCommand;
}