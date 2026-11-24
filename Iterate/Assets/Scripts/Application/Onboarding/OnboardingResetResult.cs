using Iterate.Application.Persistence;

namespace Iterate.Application.Onboarding
{
    /// <summary>
    /// The outcome of a developer onboarding reset: refused while a Session runs, otherwise the
    /// persistence reset's own result.
    /// </summary>
    /// <param name="Refused">Whether the reset was refused because a Session is active.</param>
    /// <param name="Reset">The persistence reset's result; null when refused.</param>
    public sealed record OnboardingResetResult(bool Refused, ResetOnboardingResult Reset);
}