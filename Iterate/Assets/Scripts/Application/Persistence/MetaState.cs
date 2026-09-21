namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The state that outlives every Session: whether the player has finished the onboarding. The
    /// per-checkpoint failure count lives with the checkpoint rather than here, because it belongs to
    /// one attempt at the onboarding rather than to the player.
    /// </summary>
    /// <param name="OnboardingComplete">Whether the onboarding has been completed.</param>
    public sealed record MetaState(bool OnboardingComplete)
    {
        /// <summary>
        /// The state of a player who has never finished the onboarding.
        /// </summary>
        public static MetaState Default { get; } = new(false);

        /// <summary>
        /// Derives the state of a player who has just finished the onboarding.
        /// </summary>
        /// <returns>The derived state.</returns>
        public MetaState WithOnboardingComplete() => this with { OnboardingComplete = true };

        /// <summary>
        /// Derives the state of a player whose onboarding has been reset for development.
        /// </summary>
        /// <returns>The derived state.</returns>
        public MetaState ResetOnboarding() => this with { OnboardingComplete = false };
    }
}