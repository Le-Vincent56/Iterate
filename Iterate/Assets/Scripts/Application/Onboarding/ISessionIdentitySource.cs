namespace Iterate.Application.Onboarding
{
    /// <summary>
    /// Supplies fresh identities for each ordinary Session, so replays do not repeat one another.
    /// </summary>
    public interface ISessionIdentitySource
    {
        /// <summary>
        /// Generates the identities for the next ordinary Session.
        /// </summary>
        /// <returns>A Session identity and a seed identity, both unused before.</returns>
        SessionIdentities Next();
    }
}