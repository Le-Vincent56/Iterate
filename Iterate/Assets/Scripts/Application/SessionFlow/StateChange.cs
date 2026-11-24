namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The areas of Session state an accepted command changed.
    /// </summary>
    /// <param name="Areas">The changed areas.</param>
    public readonly record struct StateChange(ChangedAreas Areas);
}