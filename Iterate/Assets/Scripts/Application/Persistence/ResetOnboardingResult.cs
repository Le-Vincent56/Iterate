namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The outcome of the developer onboarding reset. Both halves are reported because they fail
    /// independently: the flag is cleared even when the checkpoint could not be, so a caller can tell a
    /// complete reset from a partial one rather than inferring it.
    /// </summary>
    /// <param name="Checkpoint">How clearing the checkpoint ended.</param>
    /// <param name="Meta">How saving the cleared flag ended.</param>
    /// <param name="State">The meta state that was saved.</param>
    public sealed record ResetOnboardingResult(ClearResult Checkpoint, SaveResult Meta, MetaState State);
}