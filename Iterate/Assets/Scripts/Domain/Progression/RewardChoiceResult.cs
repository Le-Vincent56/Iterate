namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of taking a pool choice. A Dependency choice routes through the installer, so its
    /// rejection is carried through rather than flattened — a choice refused for RAM is a different
    /// thing from one refused for membership.
    /// </summary>
    /// <param name="Succeeded">Whether the choice was taken.</param>
    /// <param name="Rejection">Why it was refused; None on success.</param>
    /// <param name="Record">The committed transaction; null on refusal.</param>
    /// <param name="InstallationRejection">The installer's own reason, when it refused.</param>
    public sealed record RewardChoiceResult(
        bool Succeeded,
        RewardRejection Rejection,
        TransactionRecord Record,
        DependencyRejection InstallationRejection
    );
}