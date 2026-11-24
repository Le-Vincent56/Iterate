using System;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// Both scope factories for the agreement suite, with handles that do nothing on disposal. It keeps
    /// the Session, Process and Build it was handed, so the suite can read the live holders.
    /// </summary>
    public sealed class AgreementScopeFactory : ISessionScopeFactory, IProcessScopeFactory
    {
        /// <summary>
        /// The Session the latest Session scope was opened around.
        /// </summary>
        public SessionState Session { get; private set; }

        /// <summary>
        /// The Process the latest Process scope was opened around.
        /// </summary>
        public ProcessState Process { get; private set; }

        /// <summary>
        /// The Build the latest Process scope was opened around.
        /// </summary>
        public BuildState Build { get; private set; }

        /// <summary>
        /// Opens a Session scope.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <returns>A handle that does nothing on disposal.</returns>
        public IDisposable Open(SessionState session)
        {
            Session = session;
            return new Handle();
        }

        /// <summary>
        /// Opens a Process scope.
        /// </summary>
        /// <param name="sessionScope">The enclosing Session scope.</param>
        /// <param name="process">The Process.</param>
        /// <param name="build">The Build.</param>
        /// <returns>A handle that does nothing on disposal.</returns>
        public IDisposable Open(IDisposable sessionScope, ProcessState process, BuildState build)
        {
            Process = process;
            Build = build;
            return new Handle();
        }

        /// <summary>
        /// A scope handle with nothing to release.
        /// </summary>
        private sealed class Handle : IDisposable
        {
            /// <summary>
            /// Releases nothing.
            /// </summary>
            public void Dispose() { }
        }
    }
}
