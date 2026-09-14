namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed outcome of committing a Utility. A refusal writes no commitment.
    /// </summary>
    /// <param name="Succeeded">Whether the commitment was recorded.</param>
    /// <param name="AlreadyCommitted">Whether that Utility was already committed for that Process.</param>
    /// <param name="Commitment">The recorded commitment; null on refusal.</param>
    public readonly record struct UtilityCommitmentResult(
        bool Succeeded,
        bool AlreadyCommitted,
        UtilityCommitment Commitment
    );
}