namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Observes every command the surface receives, accepted or refused, after its events.
    /// </summary>
    public interface ICommandObserver
    {
        /// <summary>
        /// Receives one command and its result.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="result">Its result.</param>
        void Observed(ISessionCommand command, CommandResult result);
    }
}