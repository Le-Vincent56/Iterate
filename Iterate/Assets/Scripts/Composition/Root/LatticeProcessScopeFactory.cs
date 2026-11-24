using System;
using Didionysymus.Lattice.Runtime;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// Opens each Process scope as a child of its Session scope, which is the handle the Session scope
    /// factory returned.
    /// </summary>
    public sealed class LatticeProcessScopeFactory : IProcessScopeFactory
    {
        /// <summary>
        /// Opens a Process scope holding the Process and its Build.
        /// </summary>
        /// <param name="sessionScope">The Session scope's handle.</param>
        /// <param name="process">The Process.</param>
        /// <param name="build">The Build.</param>
        /// <returns>The child scope, which closes on disposal.</returns>
        /// <exception cref="ArgumentException">Thrown when the handle is not a scope this composition opened.</exception>
        public IDisposable Open(IDisposable sessionScope, ProcessState process, BuildState build)
        {
            if (sessionScope is not IObjectResolver parent)
                throw new ArgumentException("A Process scope opens inside a Session scope opened by this composition.", nameof(sessionScope));

            ProcessInstaller installer = new(process, build);
            return parent.CreateChildScope(builder => builder.Install(installer));
        }
    }
}