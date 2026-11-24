namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// What a command handler reports back to the command pipeline: whether the Domain accepted the
    /// operation, the Domain's own result record, and which areas of state changed.
    /// </summary>
    /// <param name="Accepted">Whether the Domain accepted the operation.</param>
    /// <param name="DomainResult">The Domain's result record, or its typed rejection.</param>
    /// <param name="Areas">The areas of state the command changed; None when refused.</param>
    public sealed record HandlerOutcome(bool Accepted, object DomainResult, ChangedAreas Areas);
}