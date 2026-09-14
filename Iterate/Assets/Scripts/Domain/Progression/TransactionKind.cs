namespace Iterate.Domain.Progression
{
    /// <summary>
    /// What an economy transaction did. Transaction identities are numbered per kind, so a Session's
    /// third purchase is "purchase:3" whatever else happened between.
    /// </summary>
    public enum TransactionKind
    {
        Purchase,
        Reroll,
        PatchAttachment,
        DependencyInstallation,
        DependencyDestruction,
        ItemDeletion,
        ItemDuplication,
        Reward
    }
}