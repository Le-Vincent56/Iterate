namespace Iterate.Application.Onboarding
{
    /// <summary>
    /// The identities an ordinary Session is begun with.
    /// </summary>
    /// <param name="SessionIdentity">The Session's identity.</param>
    /// <param name="SeedIdentity">The Session's seed identity, never shown to the player.</param>
    public sealed record SessionIdentities(string SessionIdentity, string SeedIdentity);
}