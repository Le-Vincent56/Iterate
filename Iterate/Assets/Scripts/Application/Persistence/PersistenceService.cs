using System;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Content.Json;
using Iterate.Application.Logging;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// Loads, saves and clears the three save files through the save port. Reading reports an outcome
    /// rather than throwing, because a file on disk is data the game found; a first launch and a
    /// corrupted file both leave the caller starting from a default, and only the log distinguishes
    /// them. Exactly one Warning is written per failure and nothing at all for anything that went as
    /// expected, so the log measures trouble rather than traffic.
    ///
    /// A file this build cannot read is left untouched. Writes and deletes are serialised behind one
    /// semaphore, so two saves started at once cannot interleave into a half-written file.
    /// </summary>
    public sealed class PersistenceService
    {
        private readonly ISaveFileStore _store;
        private readonly IGameLogger _logger;
        private readonly SemaphoreSlim _writes = new(1, 1);

        public PersistenceService(ISaveFileStore store, IGameLoggerFactory loggers)
        {
            if (loggers == null)
                throw new ArgumentException("A persistence service requires a logger factory.", nameof(loggers));

            _store = store ?? throw new ArgumentException("A persistence service requires a save file store.", nameof(store));
            LogCategory category = LogCategories.Persistence;
            _logger = loggers.Create(in category);
        }

        /// <summary>
        /// Loads the player's settings.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The load result.</returns>
        public Task<LoadResult<SettingsState>> LoadSettingsAsync(CancellationToken cancellationToken)
        {
            return LoadAsync(SaveFiles.Settings, SettingsStateCodec.FromJson, cancellationToken);
        }

        /// <summary>
        /// Saves the player's settings.
        /// </summary>
        /// <param name="state">The settings to save.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The save result.</returns>
        public Task<SaveResult> SaveSettingsAsync(SettingsState state, CancellationToken cancellationToken)
        {
            return SaveAsync(SaveFiles.Settings, SettingsStateCodec.ToJson(state), cancellationToken);
        }

        /// <summary>
        /// Loads the state that outlives every Session.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The load result.</returns>
        public Task<LoadResult<MetaState>> LoadMetaAsync(CancellationToken cancellationToken)
        {
            return LoadAsync(SaveFiles.Meta, MetaStateCodec.FromJson, cancellationToken);
        }

        /// <summary>
        /// Saves the state that outlives every Session.
        /// </summary>
        /// <param name="state">The state to save.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The save result.</returns>
        public Task<SaveResult> SaveMetaAsync(MetaState state, CancellationToken cancellationToken)
        {
            return SaveAsync(SaveFiles.Meta, MetaStateCodec.ToJson(state), cancellationToken);
        }

        /// <summary>
        /// Loads the onboarding checkpoint.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The load result.</returns>
        public Task<LoadResult<CheckpointState>> LoadCheckpointAsync(CancellationToken cancellationToken)
        {
            return LoadAsync(SaveFiles.Checkpoint, CheckpointStateCodec.FromJson, cancellationToken);
        }

        /// <summary>
        /// Saves the onboarding checkpoint.
        /// </summary>
        /// <param name="state">The checkpoint to save.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The save result.</returns>
        public Task<SaveResult> SaveCheckpointAsync(CheckpointState state, CancellationToken cancellationToken)
        {
            return SaveAsync(SaveFiles.Checkpoint, CheckpointStateCodec.ToJson(state), cancellationToken);
        }

        /// <summary>
        /// Removes the onboarding checkpoint.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The clear result.</returns>
        public async Task<ClearResult> ClearCheckpointAsync(CancellationToken cancellationToken)
        {
            await _writes.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                SaveFileDeleteResult deleted = await _store
                    .DeleteAsync(SaveFiles.Checkpoint, cancellationToken)
                    .ConfigureAwait(false);

                switch (deleted.Outcome)
                {
                    case SaveFileDeleteOutcome.Deleted:
                        return new ClearResult(ClearOutcome.Cleared, null);

                    case SaveFileDeleteOutcome.Absent:
                        return new ClearResult(ClearOutcome.Absent, null);

                    default:
                        Warn("Save file delete failed", SaveFiles.Checkpoint, deleted.Defect);
                        return new ClearResult(ClearOutcome.Failed, deleted.Defect);
                }
            }
            finally
            {
                _writes.Release();
            }
        }

        /// <summary>
        /// Returns the player to a first launch: removes the checkpoint, then clears the
        /// onboarding-completion flag. The order matters — clearing the flag first and then failing to
        /// remove the checkpoint would leave a first launch pointing at a finished onboarding's state.
        /// </summary>
        /// <param name="current">The meta state to derive the cleared one from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Both results and the state that was saved.</returns>
        public async Task<ResetOnboardingResult> ResetOnboardingAsync(
            MetaState current,
            CancellationToken cancellationToken
        )
        {
            if (current == null)
                throw new ArgumentException("A reset requires the current meta state.", nameof(current));

            ClearResult cleared = await ClearCheckpointAsync(cancellationToken).ConfigureAwait(false);
            MetaState reset = current.ResetOnboarding();
            SaveResult saved = await SaveMetaAsync(reset, cancellationToken).ConfigureAwait(false);
            return new ResetOnboardingResult(cleared, saved, reset);
        }

        /// <summary>
        /// Reads one file, unwraps its envelope and decodes its payload, reporting the first thing that
        /// went wrong.
        /// </summary>
        /// <typeparam name="T">The state type to decode.</typeparam>
        /// <param name="fileName">The file to load.</param>
        /// <param name="decode">The codec's decode function.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The load result.</returns>
        private async Task<LoadResult<T>> LoadAsync<T>(
            string fileName,
            Func<JsonObject, CodecResult<T>> decode,
            CancellationToken cancellationToken
        )
            where T : class
        {
            SaveFileReadResult read = await _store.ReadAsync(fileName, cancellationToken).ConfigureAwait(false);
            if (read.Outcome == SaveFileReadOutcome.Absent)
                return new LoadResult<T>(LoadOutcome.Absent, null, 0, null);

            if (read.Outcome == SaveFileReadOutcome.Unreadable)
            {
                Warn("Save file unreadable", fileName, read.Defect);
                return new LoadResult<T>(LoadOutcome.Unreadable, null, 0, read.Defect);
            }

            VersionedDocumentReadResult document = VersionedDocument.Read(read.Text, SaveFiles.FormatVersion);
            if (document.Outcome == VersionedDocumentOutcome.UnsupportedVersion)
            {
                _logger.Warning("Save file version unsupported",
                    LogField.Of("file", fileName),
                    LogField.Of("version", document.FormatVersion)
                );

                return new LoadResult<T>(LoadOutcome.UnsupportedVersion, null, document.FormatVersion, document.Reason);
            }

            if (document.Outcome == VersionedDocumentOutcome.Malformed)
            {
                Warn("Save file unreadable", fileName, document.Reason);
                return new LoadResult<T>(LoadOutcome.Unreadable, null, 0, document.Reason);
            }

            CodecResult<T> decoded = decode(document.Payload);
            if (!decoded.Succeeded)
            {
                Warn("Save file unreadable", fileName, decoded.Defect);
                return new LoadResult<T>(LoadOutcome.Unreadable, null, document.FormatVersion, decoded.Defect);
            }

            return new LoadResult<T>(LoadOutcome.Loaded, decoded.Value, document.FormatVersion, null);
        }

        /// <summary>
        /// Writes one payload inside the envelope, serialised against every other write and delete.
        /// </summary>
        /// <param name="fileName">The file to write.</param>
        /// <param name="payload">The payload to write.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The save result.</returns>
        private async Task<SaveResult> SaveAsync(
            string fileName,
            JsonObject payload,
            CancellationToken cancellationToken
        )
        {
            string text = VersionedDocument.Write(SaveFiles.FormatVersion, payload);
            await _writes.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                SaveFileWriteResult written = await _store
                    .WriteAsync(fileName, text, cancellationToken)
                    .ConfigureAwait(false);

                if (written.Succeeded)
                    return SaveResult.Saved();

                Warn("Save file write failed", fileName, written.Defect);
                return SaveResult.Failed(written.Defect);
            }
            finally
            {
                _writes.Release();
            }
        }

        /// <summary>
        /// Writes the one Warning a failure earns.
        /// </summary>
        /// <param name="message">What failed.</param>
        /// <param name="fileName">The file it happened to.</param>
        /// <param name="defect">Why it failed.</param>
        private void Warn(string message, string fileName, string defect) => _logger.Warning(message, LogField.Of("file", fileName), LogField.Of("defect", defect));
    }
}