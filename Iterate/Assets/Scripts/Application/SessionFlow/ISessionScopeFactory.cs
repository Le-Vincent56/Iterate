using System;
using Iterate.Domain.Progression;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Opens the scope that lives exactly as long as one Session. Disposing the handle ends the scope,
    /// and that disposal is the Session's reset.
    /// </summary>
    public interface ISessionScopeFactory
    {
        /// <summary>
        /// Opens a Session scope around a Session.
        /// </summary>
        /// <param name="session">The Session the scope serves.</param>
        /// <returns>The handle that closes the scope.</returns>
        IDisposable Open(SessionState session);
    }
}