using System.Collections.Generic;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// A lifecycle that writes every call into a shared ordered log and answers a failure with a
    /// settable disposition. It also notes the last allocated Session instance identity at each
    /// Process entry, which is how the capture-moment contract is observed: nothing the Process
    /// allocates may have happened yet when entry is announced.
    /// </summary>
    public sealed class RecordingSessionLifecycle : ISessionLifecycle
    {
        private readonly List<string> _log;

        /// <summary>
        /// The disposition a failure is answered with.
        /// </summary>
        public ProcessFailureDisposition Disposition { get; set; } = ProcessFailureDisposition.EndSession;

        /// <summary>
        /// The last allocated instance identity observed at each Process entry, in order.
        /// </summary>
        public List<int> LastAllocatedAtEntry { get; } = new();

        /// <summary>
        /// The route passed at each Process entry, in order.
        /// </summary>
        public List<RouteID?> RoutesAtEntry { get; } = new();

        /// <summary>
        /// Creates the lifecycle over a shared log.
        /// </summary>
        /// <param name="log">The ordered log every recording fake writes to.</param>
        public RecordingSessionLifecycle(List<string> log)
        {
            _log = log;
        }

        /// <summary>
        /// Records an entry.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The Process.</param>
        /// <param name="route">The route.</param>
        public void OnProcessEntry(SessionState session, ProcessID process, RouteID? route)
        {
            LastAllocatedAtEntry.Add(session.InstanceIDs.LastAllocated);
            RoutesAtEntry.Add(route);
            _log.Add($"entry {process.Value}");
        }

        /// <summary>
        /// Records a failure and answers with the set disposition.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The Process.</param>
        /// <param name="route">The route.</param>
        /// <returns>The set disposition.</returns>
        public ProcessFailureDisposition OnProcessFailed(SessionState session, ProcessID process, RouteID? route)
        {
            _log.Add($"failed {process.Value}");
            return Disposition;
        }

        /// <summary>
        /// Records completion.
        /// </summary>
        /// <param name="session">The Session.</param>
        public void OnSessionCompleted(SessionState session)
        {
            _log.Add("completed");
        }

        /// <summary>
        /// Records abandonment.
        /// </summary>
        /// <param name="session">The Session.</param>
        public void OnSessionAbandoned(SessionState session)
        {
            _log.Add("abandoned");
        }
    }
}
