using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Application.Persistence;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// A lifecycle for the agreement suite. At every Process entry it captures the Session's checkpoint
    /// document exactly as it would be saved, so that the document can be compared with the driver's
    /// Session at the same moment. A failure ends the Session.
    /// </summary>
    public sealed class AgreementSessionLifecycle : ISessionLifecycle
    {
        /// <summary>
        /// The checkpoint document captured at each Process entry, in order.
        /// </summary>
        public List<string> EntryDocuments { get; } = new();

        /// <summary>
        /// The Process entered at each capture, in order.
        /// </summary>
        public List<ProcessID> EnteredProcesses { get; } = new();

        /// <summary>
        /// Captures the Session's checkpoint document at entry.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The Process being entered.</param>
        /// <param name="route">The committed route, or null.</param>
        public void OnProcessEntry(SessionState session, ProcessID process, RouteID? route)
        {
            EnteredProcesses.Add(process);
            EntryDocuments.Add(Document(session, process, route));
        }

        /// <summary>
        /// Ends the Session on any failure.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The failed Process.</param>
        /// <param name="route">The committed route, or null.</param>
        /// <returns>Always <see cref="ProcessFailureDisposition.EndSession"/>.</returns>
        public ProcessFailureDisposition OnProcessFailed(SessionState session, ProcessID process, RouteID? route)
        {
            return ProcessFailureDisposition.EndSession;
        }

        /// <summary>
        /// Ignores completion.
        /// </summary>
        /// <param name="session">The Session.</param>
        public void OnSessionCompleted(SessionState session) { }

        /// <summary>
        /// Ignores abandonment.
        /// </summary>
        /// <param name="session">The Session.</param>
        public void OnSessionAbandoned(SessionState session) { }

        /// <summary>
        /// The checkpoint document a Session would be saved as at a Process entry.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The Process being entered.</param>
        /// <param name="route">The committed route, or null.</param>
        /// <returns>The document text, or the capture's rejection when it cannot be captured.</returns>
        public static string Document(SessionState session, ProcessID process, RouteID? route)
        {
            CheckpointCaptureResult captured = CheckpointCapture.Capture(session, process, route, 0);
            if (!captured.Succeeded)
                return $"capture refused: {captured.Rejection}";

            return VersionedDocument.Write(SaveFiles.FormatVersion, CheckpointStateCodec.ToJson(captured.State));
        }
    }
}
