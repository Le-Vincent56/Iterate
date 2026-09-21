namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The outcome of clearing the checkpoint.
    /// </summary>
    /// <param name="Outcome">How the clear ended.</param>
    /// <param name="Defect">A sentence naming what went wrong, or null.</param>
    public sealed record ClearResult(ClearOutcome Outcome, string Defect);
}