namespace Iterate.Application.Onboarding
{
    /// <summary>
    /// Which lifecycle a Session runs under.
    /// </summary>
    public enum SessionMode
    {
        /// <summary>
        /// The first-time onboarding: checkpoints are saved and failures recover by role.
        /// </summary>
        Onboarding,

        /// <summary>
        /// An ordinary Session: nothing is saved and a failure ends the Session.
        /// </summary>
        Ordinary,
    }
}