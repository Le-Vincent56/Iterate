using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Persistence;
using Iterate.Application.Persistence.Tests;

namespace Iterate.Application.Onboarding.Tests
{
    /// <summary>
    /// A save store whose writes can be held open, so a suite can queue several saves before any
    /// completes and then check the order and content they land in. Reads and deletes pass straight
    /// through to the inner fake.
    /// </summary>
    public sealed class GatedSaveFileStore : ISaveFileStore
    {
        private TaskCompletionSource<bool> _gate = CompletedGate();

        /// <summary>
        /// The fake the writes land in.
        /// </summary>
        public FakeSaveFileStore Inner { get; } = new();

        /// <summary>
        /// The text of every write to the checkpoint file, in the order the writes landed.
        /// </summary>
        public List<string> CheckpointWrites { get; } = new();

        /// <summary>
        /// Holds every write that starts from now until <see cref="Release"/>.
        /// </summary>
        public void Hold()
        {
            _gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>
        /// Lets every held write proceed.
        /// </summary>
        public void Release()
        {
            _gate.TrySetResult(true);
        }

        /// <summary>
        /// Reads through the inner fake.
        /// </summary>
        /// <param name="fileName">The file.</param>
        /// <param name="cancellationToken">Ignored.</param>
        /// <returns>The read result.</returns>
        public Task<SaveFileReadResult> ReadAsync(string fileName, CancellationToken cancellationToken)
        {
            return Inner.ReadAsync(fileName, cancellationToken);
        }

        /// <summary>
        /// Waits for the gate, then writes through the inner fake.
        /// </summary>
        /// <param name="fileName">The file.</param>
        /// <param name="text">The text.</param>
        /// <param name="cancellationToken">Ignored.</param>
        /// <returns>The write result.</returns>
        public async Task<SaveFileWriteResult> WriteAsync(string fileName, string text, CancellationToken cancellationToken)
        {
            await _gate.Task.ConfigureAwait(false);
            if (fileName == SaveFiles.Checkpoint)
                CheckpointWrites.Add(text);

            return await Inner.WriteAsync(fileName, text, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes through the inner fake.
        /// </summary>
        /// <param name="fileName">The file.</param>
        /// <param name="cancellationToken">Ignored.</param>
        /// <returns>The delete result.</returns>
        public Task<SaveFileDeleteResult> DeleteAsync(string fileName, CancellationToken cancellationToken)
        {
            return Inner.DeleteAsync(fileName, cancellationToken);
        }

        /// <summary>
        /// A gate that is already open.
        /// </summary>
        /// <returns>The open gate.</returns>
        private static TaskCompletionSource<bool> CompletedGate()
        {
            TaskCompletionSource<bool> gate = new();
            gate.SetResult(true);
            return gate;
        }
    }
}
