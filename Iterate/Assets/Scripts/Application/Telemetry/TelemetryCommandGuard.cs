using System;
using Iterate.Application.SessionFlow;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Decorates the command surface for telemetry (UX-VAL-029). It tells the recorder each command
    /// is starting and, when Session Flow throws, has it record the command as an unrecoverable
    /// error before rethrowing the same exception. It has no other effect on play.
    /// </summary>
    public sealed class TelemetryCommandGuard : ISessionCommands
    {
        private readonly ISessionCommands _inner;
        private readonly TelemetryRecorder _recorder;

        /// <summary>
        /// The command surface this guard decorates.
        /// </summary>
        public ISessionCommands Inner => _inner;

        /// <summary>
        /// Creates the guard.
        /// </summary>
        /// <param name="inner">The command surface; Composition passes the Session Flow singleton.</param>
        /// <param name="recorder">The telemetry recorder.</param>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public TelemetryCommandGuard(ISessionCommands inner, TelemetryRecorder recorder)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        }

        /// <summary>
        /// Executes a command through the inner surface.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The inner surface's result.</returns>
        public CommandResult Execute(ISessionCommand command)
        {
            _recorder.CommandStarting();
            try
            {
                return _inner.Execute(command);
            }
            catch (Exception exception)
            {
                _recorder.CommandThrew(command, exception);
                throw;
            }
        }
    }
}