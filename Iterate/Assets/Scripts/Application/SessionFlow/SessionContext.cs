using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The mutable holder set a Session runs on, owned by the Session's command pipeline and written
    /// only while a command or a Session begin is applied. It is public so a route's handler signature
    /// can name it; its constructor and every member are internal, so outside this assembly it is an
    /// opaque type nothing can create or read.
    /// </summary>
    public sealed class SessionContext
    {
        /// <summary>
        /// The catalog the Session was begun against, taken once per Session.
        /// </summary>
        internal ContentCatalog Catalog { get; set; }

        /// <summary>
        /// The System's plan.
        /// </summary>
        internal StagePlan Plan { get; set; }

        /// <summary>
        /// The Session.
        /// </summary>
        internal SessionState Session { get; set; }

        /// <summary>
        /// The Process in play, or null outside one.
        /// </summary>
        internal ProcessState Process { get; set; }

        /// <summary>
        /// The Process's Build, or null outside a Process.
        /// </summary>
        internal BuildState Build { get; set; }

        /// <summary>
        /// The open shop, or null outside one.
        /// </summary>
        internal ShopState Shop { get; set; }

        /// <summary>
        /// The execution result under review, or null when none is.
        /// </summary>
        internal CurrentResult Result { get; set; }

        /// <summary>
        /// The submitted Process's reward resolution, or null outside the rewards stage.
        /// </summary>
        internal RewardResolution Rewards { get; set; }

        /// <summary>
        /// The Session scope's handle, or null when no Session is active.
        /// </summary>
        internal IDisposable SessionScope { get; set; }

        /// <summary>
        /// The Process scope's handle, or null outside a Process.
        /// </summary>
        internal IDisposable ProcessScope { get; set; }

        /// <summary>
        /// The stage the Session stands in.
        /// </summary>
        internal SessionStage Stage { get; set; }

        /// <summary>
        /// Why the Session ended; None while it runs.
        /// </summary>
        internal SessionEndReason EndReason { get; set; }

        /// <summary>
        /// Whether a Session is in progress.
        /// </summary>
        internal bool IsActive => Stage != SessionStage.NoSession && Stage != SessionStage.SessionEnded;

        /// <summary>
        /// Creates an empty context with no Session.
        /// </summary>
        internal SessionContext()
        {
            Stage = SessionStage.NoSession;
            EndReason = SessionEndReason.None;
        }

        /// <summary>
        /// Drops every holder and handle; the stage and end reason are left as they are.
        /// </summary>
        internal void ClearHolders()
        {
            Catalog = null;
            Plan = null;
            Session = null;
            Process = null;
            Build = null;
            Shop = null;
            Result = null;
            Rewards = null;
            SessionScope = null;
            ProcessScope = null;
        }
    }
}