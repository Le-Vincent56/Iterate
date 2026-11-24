using System;
using Iterate.Application.SessionFlow;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Session Flow's command observer for telemetry. It has no dependencies, so Session Flow can be
    /// built over it while the recorder, which reads Session Flow, subscribes to it afterwards.
    /// </summary>
    public sealed class TelemetryCommandTap : ICommandObserver
    {
        /// <summary>
        /// Raised once for each finished command, with the same command and result instances.
        /// </summary>
        public event Action<CommandObservation> CommandObserved;

        /// <summary>
        /// Raises <see cref="CommandObserved"/>.
        /// </summary>
        /// <param name="command">The finished command.</param>
        /// <param name="result">Its result.</param>
        public void Observed(ISessionCommand command, CommandResult result)
        {
            CommandObserved?.Invoke(new CommandObservation(command, result));
        }
    }
}