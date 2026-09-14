namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed outcome of a rack operation. A refusal changes nothing: no reservation, no release,
    /// no entry.
    /// </summary>
    /// <param name="Succeeded">Whether the operation was applied.</param>
    /// <param name="Rejection">Why it was refused; None on success.</param>
    /// <param name="Entry">The installed entry added or removed; null on refusal.</param>
    /// <param name="RAMChanged">The RAM reserved or released.</param>
    public readonly record struct RackResult(
        bool Succeeded,
        DependencyRejection Rejection,
        InstalledDependency Entry,
        int RAMChanged
    );
}