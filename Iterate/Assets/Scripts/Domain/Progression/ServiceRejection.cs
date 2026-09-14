namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Why a Repository Service was refused. Starter protection is checked before price, so a player
    /// asking to delete a protected item is told that rather than told to find Tokens for something
    /// they could never buy.
    /// </summary>
    public enum ServiceRejection
    {
        None,
        UnknownInstance,
        StarterProtected,
        InsufficientTokens,
        ServicesNotOffered
    }
}