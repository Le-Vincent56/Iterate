using System;
using System.Collections.Generic;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Both scope factories in one fake: every open and every disposal goes into a shared ordered log,
    /// and the holders each scope was opened around are kept, which is how a suite reaches the live
    /// Session, Process and Build without any read path into the Session surface.
    /// </summary>
    public sealed class RecordingScopeFactory : ISessionScopeFactory, IProcessScopeFactory
    {
        private readonly List<string> _log;

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
        /// How many Session scopes have opened.
        /// </summary>
        public int SessionScopesOpened { get; private set; }

        /// <summary>
        /// How many Process scopes have opened.
        /// </summary>
        public int ProcessScopesOpened { get; private set; }

        /// <summary>
        /// How many scopes of either kind are open now.
        /// </summary>
        public int OpenScopes { get; private set; }

        /// <summary>
        /// Creates the factory over a shared log.
        /// </summary>
        /// <param name="log">The ordered log every recording fake writes to.</param>
        public RecordingScopeFactory(List<string> log)
        {
            _log = log;
        }

        /// <summary>
        /// Opens and records a Session scope.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <returns>A handle recording its disposal.</returns>
        public IDisposable Open(SessionState session)
        {
            Session = session;
            SessionScopesOpened++;
            OpenScopes++;
            _log.Add("open session scope");
            return new RecordingScope(this, "session");
        }

        /// <summary>
        /// Opens and records a Process scope.
        /// </summary>
        /// <param name="sessionScope">The enclosing Session scope.</param>
        /// <param name="process">The Process.</param>
        /// <param name="build">The Build.</param>
        /// <returns>A handle recording its disposal.</returns>
        public IDisposable Open(IDisposable sessionScope, ProcessState process, BuildState build)
        {
            Process = process;
            Build = build;
            ProcessScopesOpened++;
            OpenScopes++;
            _log.Add($"open process scope {process.ProcessIdentity}");
            return new RecordingScope(this, "process");
        }

        /// <summary>
        /// Records one disposal.
        /// </summary>
        /// <param name="kind">Which scope closed.</param>
        private void Closed(string kind)
        {
            OpenScopes--;
            _log.Add($"dispose {kind} scope");
        }

        /// <summary>
        /// A scope handle that reports its first disposal to the factory.
        /// </summary>
        private sealed class RecordingScope : IDisposable
        {
            private readonly RecordingScopeFactory _owner;
            private readonly string _kind;
            private bool _disposed;

            /// <summary>
            /// Creates the handle.
            /// </summary>
            /// <param name="owner">The factory to report to.</param>
            /// <param name="kind">Which scope this is.</param>
            public RecordingScope(RecordingScopeFactory owner, string kind)
            {
                _owner = owner;
                _kind = kind;
            }

            /// <summary>
            /// Reports the first disposal; later ones are ignored.
            /// </summary>
            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _owner.Closed(_kind);
            }
        }
    }
}
