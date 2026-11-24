using System.Collections.Generic;
using Iterate.Application.Content;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// A Session surface over the fixture catalog with every port recorded into one ordered log,
    /// events included, so a suite can assert what happened and in what order. The stage is tracked
    /// from the stage-change events, which is the only stage a presenter would see.
    /// </summary>
    public sealed class SessionFlowRig
    {
        /// <summary>
        /// The ordered log every fake and event writes to.
        /// </summary>
        public List<string> Log { get; } = new();

        /// <summary>
        /// The lifecycle.
        /// </summary>
        public RecordingSessionLifecycle Lifecycle { get; }

        /// <summary>
        /// Both scope factories.
        /// </summary>
        public RecordingScopeFactory Scopes { get; }

        /// <summary>
        /// The gates, in registration order.
        /// </summary>
        public List<RecordingCommandGate> Gates { get; } = new();

        /// <summary>
        /// The observers, in registration order.
        /// </summary>
        public List<RecordingCommandObserver> Observers { get; } = new();

        /// <summary>
        /// The stage changes raised, in order.
        /// </summary>
        public List<StageChange> StageChanges { get; } = new();

        /// <summary>
        /// The state changes raised, in order.
        /// </summary>
        public List<StateChange> StateChanges { get; } = new();

        /// <summary>
        /// The Session surface.
        /// </summary>
        public SessionFlow Flow { get; }

        /// <summary>
        /// The queries over the same Session.
        /// </summary>
        public SessionQueryService Queries { get; }

        /// <summary>
        /// The stage the latest stage change entered.
        /// </summary>
        public SessionStage Stage { get; private set; } = SessionStage.NoSession;

        /// <summary>
        /// Builds the rig over a loaded fixture catalog, with the given numbers of permitting gates and
        /// observers.
        /// </summary>
        /// <param name="gates">How many gates to register.</param>
        /// <param name="observers">How many observers to register.</param>
        public SessionFlowRig(int gates = 0, int observers = 1) : this(SessionFlowFixtures.LoadedHolder(), gates, observers)
        {
        }

        /// <summary>
        /// Builds the rig over a given catalog holder.
        /// </summary>
        /// <param name="holder">The catalog holder.</param>
        /// <param name="gates">How many gates to register.</param>
        /// <param name="observers">How many observers to register.</param>
        public SessionFlowRig(CatalogHolder holder, int gates, int observers)
        {
            Lifecycle = new RecordingSessionLifecycle(Log);
            Scopes = new RecordingScopeFactory(Log);
            for (int index = 0; index < gates; index++)
                Gates.Add(new RecordingCommandGate(Log, $"G{index}", true));

            for (int index = 0; index < observers; index++)
                Observers.Add(new RecordingCommandObserver(Log, $"O{index}"));

            Flow = new SessionFlow(
                holder,
                Lifecycle,
                Scopes,
                Scopes,
                new List<ICommandGate>(Gates),
                new List<ICommandObserver>(Observers)
            );

            Queries = new SessionQueryService(Flow);
            Flow.StageChanged += OnStageChanged;
            Flow.StateChanged += OnStateChanged;
        }

        /// <summary>
        /// Begins a fresh Session over the fixture archetype.
        /// </summary>
        /// <param name="system">The System to play.</param>
        /// <returns>The begin result.</returns>
        public SessionBeginResult Begin(SystemID system)
        {
            return Flow.BeginSession(SessionFlowFixtures.Archetype(), system, "session-1", "seed-1");
        }

        /// <summary>
        /// Begins a fresh Session over the play archetype, whose whole starter Repository every play
        /// Process loads into its Buffer.
        /// </summary>
        /// <param name="system">The System to play.</param>
        /// <returns>The begin result.</returns>
        public SessionBeginResult BeginPlay(SystemID system)
        {
            return Flow.BeginSession(SessionFlowPlayContent.Archetype(), system, "session-1", "seed-1");
        }

        /// <summary>
        /// Records a stage change.
        /// </summary>
        /// <param name="change">The change.</param>
        private void OnStageChanged(StageChange change)
        {
            Stage = change.Current;
            StageChanges.Add(change);
            Log.Add($"stage {change.Previous} -> {change.Current}");
        }

        /// <summary>
        /// Records a state change.
        /// </summary>
        /// <param name="change">The change.</param>
        private void OnStateChanged(StateChange change)
        {
            StateChanges.Add(change);
            Log.Add($"state {change.Areas}");
        }
    }
}
