namespace Iterate.Application.Onboarding
{
    /// <summary>
    /// What a launch begins: onboarding, possibly resuming at its saved checkpoint, or an ordinary
    /// Session.
    /// </summary>
    /// <param name="Mode">Onboarding until it is complete, ordinary afterwards.</param>
    /// <param name="ResumesAtCheckpoint">Whether onboarding resumes at a saved checkpoint rather than from the start.</param>
    public sealed record LaunchDecision(SessionMode Mode, bool ResumesAtCheckpoint);
}