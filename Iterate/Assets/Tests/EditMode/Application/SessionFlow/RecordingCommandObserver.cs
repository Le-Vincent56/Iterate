using System.Collections.Generic;
using Iterate.Application.SessionFlow;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// An observer that writes each observation into a shared ordered log and keeps the results.
    /// </summary>
    public sealed class RecordingCommandObserver : ICommandObserver
    {
        private readonly List<string> _log;
        private readonly string _name;

        /// <summary>
        /// The results observed, in order.
        /// </summary>
        public List<CommandResult> Results { get; } = new();

        /// <summary>
        /// Creates the observer.
        /// </summary>
        /// <param name="log">The ordered log every recording fake writes to.</param>
        /// <param name="name">The name the log records it by.</param>
        public RecordingCommandObserver(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        /// <summary>
        /// Records the observation.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="result">Its result.</param>
        public void Observed(ISessionCommand command, CommandResult result)
        {
            Results.Add(result);
            _log.Add($"observer {_name} {command.GetType().Name} {result.Rejection}");
        }
    }
}
