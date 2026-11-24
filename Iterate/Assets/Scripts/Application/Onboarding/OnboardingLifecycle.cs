using System;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Content;
using Iterate.Application.Logging;
using Iterate.Application.Persistence;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Onboarding
{
    /// <summary>
    /// The first-time onboarding lifecycle. Every Process entry saves a checkpoint, and every failure
    /// counts against it and recovers by the Process's role: a tutorial restarts, and a later Process
    /// returns to Branch configuration. Completion records onboarding as done and clears the
    /// checkpoint; abandonment clears it.
    ///
    /// The count belongs to the checkpoint: it is kept when the same Process is entered again after a
    /// failure, and starts over at any other Process. The port is synchronous, so saves are queued
    /// rather than awaited. Each queued operation carries the values it saves, captured when it was
    /// queued, and runs only after the one before it. A failed save is recorded and logged; play
    /// continues.
    /// </summary>
    public sealed class OnboardingLifecycle : ISessionLifecycle
    {
        private readonly PersistenceService _persistence;
        private readonly CatalogHolder _catalog;
        private readonly IGameLogger _logger;
        private CheckpointState _checkpoint;
        private SaveResult _lastSaveResult;
        private Task _pendingWork = Task.CompletedTask;

        /// <summary>
        /// The consecutive failures at the current checkpoint; zero when there is none.
        /// </summary>
        public int ConsecutiveFailures => _checkpoint?.ConsecutiveFailures ?? 0;

        /// <summary>
        /// The result of the most recent save to complete, or null before any has.
        /// </summary>
        public SaveResult LastSaveResult => Volatile.Read(ref _lastSaveResult);

        /// <summary>
        /// Completes once every queued save and clear has finished.
        /// </summary>
        public Task PendingWork => _pendingWork;

        /// <summary>
        /// Creates the lifecycle over the persistence service and the catalog.
        /// </summary>
        /// <param name="persistence">The persistence service the saves go through.</param>
        /// <param name="catalog">The holder the Process configurations are read from.</param>
        /// <param name="loggers">The logger factory.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public OnboardingLifecycle(PersistenceService persistence, CatalogHolder catalog, IGameLoggerFactory loggers)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (loggers == null)
                throw new ArgumentNullException(nameof(loggers));

            LogCategory category = LogCategories.Persistence;
            _logger = loggers.Create(in category);
        }

        /// <summary>
        /// Seeds the lifecycle with a restored checkpoint, so re-entering its Process keeps its count.
        /// </summary>
        /// <param name="checkpoint">The restored checkpoint.</param>
        /// <exception cref="ArgumentNullException">Thrown when the checkpoint is null.</exception>
        public void Resume(CheckpointState checkpoint)
        {
            _checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
        }

        /// <summary>
        /// Drops the current checkpoint; the count starts over.
        /// </summary>
        public void Forget()
        {
            _checkpoint = null;
        }

        /// <summary>
        /// Captures and saves the checkpoint at a Process entry.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The Process being entered.</param>
        /// <param name="route">The committed route, or null.</param>
        public void OnProcessEntry(SessionState session, ProcessID process, RouteID? route)
        {
            int failures = _checkpoint != null && _checkpoint.Process == process ? _checkpoint.ConsecutiveFailures : 0;
            CheckpointCaptureResult captured = CheckpointCapture.Capture(session, process, route, failures);
            if (!captured.Succeeded)
            {
                _logger.Warning("Onboarding checkpoint not captured", LogField.Of("process", process.Value), LogField.Of("rejection", captured.Rejection.ToString()));
                return;
            }

            _checkpoint = captured.State;
            CheckpointState state = captured.State;
            Enqueue(() => SaveCheckpointAsync(state));
        }

        /// <summary>
        /// Counts a failure against the checkpoint, saves it, and answers with the recovery the
        /// Process's role calls for.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The failed Process.</param>
        /// <param name="route">The committed route, or null.</param>
        /// <returns>A restart for a tutorial, a return to Branch configuration otherwise.</returns>
        public ProcessFailureDisposition OnProcessFailed(SessionState session, ProcessID process, RouteID? route)
        {
            if (_checkpoint != null && _checkpoint.Process == process)
            {
                _checkpoint = _checkpoint.RecordFailure();
                CheckpointState state = _checkpoint;
                Enqueue(() => SaveCheckpointAsync(state));
            }

            return DispositionFor(process);
        }

        /// <summary>
        /// Records onboarding as complete, and clears the checkpoint once that is saved.
        /// </summary>
        /// <param name="session">The Session.</param>
        public void OnSessionCompleted(SessionState session)
        {
            _checkpoint = null;
            Enqueue(CompleteAsync);
        }

        /// <summary>
        /// Clears the checkpoint of an abandoned onboarding.
        /// </summary>
        /// <param name="session">The Session.</param>
        public void OnSessionAbandoned(SessionState session)
        {
            _checkpoint = null;
            Enqueue(ClearAsync);
        }

        /// <summary>
        /// The recovery a Process's role calls for.
        /// </summary>
        /// <param name="process">The Process.</param>
        /// <returns>The disposition.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the catalog does not define the Process.</exception>
        private ProcessFailureDisposition DispositionFor(ProcessID process)
        {
            if (!_catalog.TryGetCatalog(out ContentCatalog catalog) || !catalog.TryGetProcessConfiguration(process, out ProcessConfigurationDefinition configuration))
                throw new InvalidOperationException($"The catalog defines no configuration for {process.Value}, which failed.");

            return configuration.Role is ProcessRole.Tutorial1 or ProcessRole.Tutorial2
                ? ProcessFailureDisposition.RestartProcess
                : ProcessFailureDisposition.ReturnToBranchConfiguration;
        }

        /// <summary>
        /// Queues an operation behind every operation already queued.
        /// </summary>
        /// <param name="work">The operation.</param>
        private void Enqueue(Func<Task> work)
        {
            _pendingWork = RunAfterAsync(_pendingWork, work);
        }

        /// <summary>
        /// Runs an operation once the previous one has finished, logging rather than throwing on fault.
        /// </summary>
        /// <param name="previous">The operation queued before.</param>
        /// <param name="work">The operation.</param>
        /// <returns>The operation's completion.</returns>
        private async Task RunAfterAsync(Task previous, Func<Task> work)
        {
            await previous.ConfigureAwait(false);
            try
            {
                await work().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error("Onboarding save work failed", exception);
            }
        }

        /// <summary>
        /// Saves a checkpoint and records the result.
        /// </summary>
        /// <param name="state">The checkpoint captured when the save was queued.</param>
        /// <returns>The save's completion.</returns>
        private async Task SaveCheckpointAsync(CheckpointState state)
        {
            SaveResult saved = await _persistence.SaveCheckpointAsync(state, CancellationToken.None).ConfigureAwait(false);
            Volatile.Write(ref _lastSaveResult, saved);
        }

        /// <summary>
        /// Saves onboarding as complete and clears the checkpoint only when that save succeeded, so a
        /// failed save leaves the last checkpoint to resume from.
        /// </summary>
        /// <returns>The operation's completion.</returns>
        private async Task CompleteAsync()
        {
            SaveResult saved = await _persistence.SaveMetaAsync(MetaState.Default.WithOnboardingComplete(), CancellationToken.None).ConfigureAwait(false);
            Volatile.Write(ref _lastSaveResult, saved);
            if (saved.Outcome == SaveOutcome.Saved)
                await _persistence.ClearCheckpointAsync(CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Clears the checkpoint.
        /// </summary>
        /// <returns>The operation's completion.</returns>
        private async Task ClearAsync()
        {
            await _persistence.ClearCheckpointAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}