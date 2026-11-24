namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Why the command surface refused a command. A refusal by the Domain carries the Domain's own
    /// typed rejection in the result; Session Flow declares no second vocabulary for it.
    /// </summary>
    public enum CommandRejection
    {
        /// <summary>
        /// The command was accepted.
        /// </summary>
        None,

        /// <summary>
        /// The command is not legal in the current stage; nothing was touched.
        /// </summary>
        WrongStage,

        /// <summary>
        /// A command gate refused the command; nothing was touched.
        /// </summary>
        Gated,

        /// <summary>
        /// The Domain refused the operation; its rejection is the result's Domain result.
        /// </summary>
        DomainRejected,

        /// <summary>
        /// No Session is active.
        /// </summary>
        NoSession,
    }
}