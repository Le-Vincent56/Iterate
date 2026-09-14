namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of an installation. A refusal carries no record and no installation.
    /// </summary>
    /// <param name="Succeeded">Whether the Dependency was installed.</param>
    /// <param name="Rejection">Why it was refused; None on success.</param>
    /// <param name="Record">The committed transaction; null on refusal.</param>
    /// <param name="Installed">The installed entry; null on refusal.</param>
    public sealed record DependencyInstallationResult(
        bool Succeeded,
        DependencyRejection Rejection,
        TransactionRecord Record,
        InstalledDependency Installed
    );
}