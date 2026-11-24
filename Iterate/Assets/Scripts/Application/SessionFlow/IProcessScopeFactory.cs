using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Opens the scope that lives exactly as long as one Process, nested in its Session's scope.
    /// </summary>
    public interface IProcessScopeFactory
    {
        /// <summary>
        /// Opens a Process scope inside a Session scope.
        /// </summary>
        /// <param name="sessionScope">The enclosing Session scope's handle.</param>
        /// <param name="process">The Process the scope serves.</param>
        /// <param name="build">The Process's Build.</param>
        /// <returns>The handle that closes the scope.</returns>
        IDisposable Open(IDisposable sessionScope, ProcessState process, BuildState build);
    }
}