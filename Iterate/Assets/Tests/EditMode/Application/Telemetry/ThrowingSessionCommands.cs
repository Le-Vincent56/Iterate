using System;
using Iterate.Application.SessionFlow;
using Iterate.Application.SessionFlow.Tests;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// A command surface that reports a Process entry through the rig's lifecycle tap, so the
    /// recorder buffers a consequence, and then throws.
    /// </summary>
    public sealed class ThrowingSessionCommands : ISessionCommands
    {
        private readonly TelemetryRig _rig;

        /// <summary>
        /// The exception thrown, created once so the caller's can be compared by instance.
        /// </summary>
        public InvalidOperationException Thrown { get; } = new("a message telemetry must never write");

        /// <summary>
        /// Creates the surface over a rig.
        /// </summary>
        /// <param name="rig">The rig whose tap is raised.</param>
        public ThrowingSessionCommands(TelemetryRig rig)
        {
            _rig = rig;
        }

        /// <summary>
        /// Reports a Process entry, then throws.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>Never returns.</returns>
        public CommandResult Execute(ISessionCommand command)
        {
            _rig.LifecycleTap.OnProcessEntry(_rig.Scopes.Session, SessionFlowFixtures.TutorialTwo, null);
            throw Thrown;
        }
    }
}
