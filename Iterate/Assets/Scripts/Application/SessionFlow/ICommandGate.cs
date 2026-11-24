namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// A gate that may refuse a legal command before it applies, such as a tutorial script holding
    /// the player to one action.
    /// </summary>
    public interface ICommandGate
    {
        /// <summary>
        /// Whether the command may apply.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="stage">The stage it was raised in.</param>
        /// <returns>True to let it apply.</returns>
        bool Permit(ISessionCommand command, SessionStage stage);
    }
}