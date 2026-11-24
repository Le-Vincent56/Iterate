using System.Collections.Generic;
using Iterate.Application.SessionFlow;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// A gate with a fixed answer that writes each consultation into a shared ordered log.
    /// </summary>
    public sealed class RecordingCommandGate : ICommandGate
    {
        private readonly List<string> _log;
        private readonly string _name;

        /// <summary>
        /// Whether the gate permits commands.
        /// </summary>
        public bool Permits { get; set; }

        /// <summary>
        /// Creates the gate.
        /// </summary>
        /// <param name="log">The ordered log every recording fake writes to.</param>
        /// <param name="name">The name the log records it by.</param>
        /// <param name="permits">Whether it permits commands.</param>
        public RecordingCommandGate(List<string> log, string name, bool permits)
        {
            _log = log;
            _name = name;
            Permits = permits;
        }

        /// <summary>
        /// Records the consultation and answers.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="stage">The stage.</param>
        /// <returns>The fixed answer.</returns>
        public bool Permit(ISessionCommand command, SessionStage stage)
        {
            _log.Add($"gate {_name} {command.GetType().Name} {stage}");
            return Permits;
        }
    }
}
