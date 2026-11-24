namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// A stage movement.
    /// </summary>
    /// <param name="Previous">The stage left.</param>
    /// <param name="Current">The stage entered.</param>
    /// <param name="EndReason">Why the Session ended; None unless the stage entered is the end.</param>
    public readonly record struct StageChange(SessionStage Previous, SessionStage Current, SessionEndReason EndReason);
}