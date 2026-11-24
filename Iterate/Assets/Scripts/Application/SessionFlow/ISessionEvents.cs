using System;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// What the Session announces after a change, once its state is consistent. Payloads carry no
    /// state; a subscriber reads what it needs from the queries.
    /// </summary>
    public interface ISessionEvents
    {
        /// <summary>
        /// Raised when the stage moves.
        /// </summary>
        event Action<StageChange> StageChanged;

        /// <summary>
        /// Raised after an accepted command, naming the areas it changed.
        /// </summary>
        event Action<StateChange> StateChanged;
    }
}