using System;
using Didionysymus.Lattice.Runtime;
using Iterate.Domain.Progression;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// Registers the holder a Session scope serves: the Session itself, as the instance it was opened
    /// around. Disposing the scope is the Session's reset.
    /// </summary>
    public sealed class SessionInstaller : IInstaller
    {
        private readonly SessionState _session;

        /// <summary>
        /// Creates the installer for one Session.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <exception cref="ArgumentNullException">Thrown when the Session is null.</exception>
        public SessionInstaller(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// Registers the Session.
        /// </summary>
        /// <param name="builder">The Session scope's builder.</param>
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_session);
        }
    }
}