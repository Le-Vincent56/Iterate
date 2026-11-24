using System;
using Didionysymus.Lattice.Runtime;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Progression;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// Opens each Session scope as a child of the project scope. This factory and the Process factory
    /// are the only types that hold a resolver; the handle each returns is the child scope itself, so
    /// disposing it ends the scope.
    /// </summary>
    public sealed class LatticeSessionScopeFactory : ISessionScopeFactory
    {
        private readonly IObjectResolver _projectScope;

        /// <summary>
        /// Creates the factory over the project scope.
        /// </summary>
        /// <param name="projectScope">The project scope's resolver.</param>
        /// <exception cref="ArgumentNullException">Thrown when the resolver is null.</exception>
        public LatticeSessionScopeFactory(IObjectResolver projectScope)
        {
            _projectScope = projectScope ?? throw new ArgumentNullException(nameof(projectScope));
        }

        /// <summary>
        /// Opens a Session scope holding the Session.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <returns>The child scope, which closes on disposal.</returns>
        public IDisposable Open(SessionState session)
        {
            SessionInstaller installer = new(session);
            return _projectScope.CreateChildScope(builder => builder.Install(installer));
        }
    }
}