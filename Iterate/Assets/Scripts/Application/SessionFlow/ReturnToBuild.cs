namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Leaves a below-Pass result for Build without confirmation; nothing is discarded, because a
    /// below-Pass result cannot be submitted.
    /// </summary>
    public sealed record ReturnToBuild : ISessionCommand;
}