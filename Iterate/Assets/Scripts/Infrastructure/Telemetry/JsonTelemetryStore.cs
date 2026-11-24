using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Logging;
using Iterate.Application.Telemetry;

namespace Iterate.Infrastructure.Telemetry
{
    /// <summary>
    /// Stores telemetry runs as <c>root/run.jsonl</c> (TA-TRC-006). Lines are appended as UTF-8
    /// without a byte-order mark and flushed to the operating system after each, so a crash keeps
    /// every line written so far; no physical-disk sync is paid on the command path. Another reader
    /// can read a run while it is open. An I/O failure logs one Warning for the run and drops its
    /// later lines; nothing is thrown. Deleting a run removes only that run's file.
    /// </summary>
    public sealed class JsonlTelemetryStore : ITelemetryWriter, ITelemetryRunStore
    {
        private const string Extension = ".jsonl";

        private static readonly UTF8Encoding _utf8 = new(false);

        private readonly string _root;
        private readonly IGameLogger _logger;
        private readonly Dictionary<string, FileStream> _open = new(StringComparer.Ordinal);
        private readonly HashSet<string> _failed = new(StringComparer.Ordinal);

        /// <summary>
        /// Creates the store over a folder, which is created when the first run opens.
        /// </summary>
        /// <param name="root">The folder runs are stored in.</param>
        /// <param name="loggers">The logger factory.</param>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public JsonlTelemetryStore(string root, IGameLoggerFactory loggers)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            if (loggers == null)
                throw new ArgumentNullException(nameof(loggers));

            LogCategory category = LogCategories.Telemetry;
            _logger = loggers.Create(in category);
        }

        /// <summary>
        /// Creates the folder and opens the run's file for appending, shared for reading.
        /// </summary>
        /// <param name="run">The run identity.</param>
        public void Open(string run)
        {
            if (_failed.Contains(run) || _open.ContainsKey(run))
                return;

            try
            {
                Directory.CreateDirectory(_root);
                _open[run] = new FileStream(PathOf(run), FileMode.Append, FileAccess.Write, FileShare.Read);
            }
            catch (IOException exception)
            {
                Fail(run, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                Fail(run, exception);
            }
        }

        /// <summary>
        /// Appends one line and flushes it to the operating system.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="line">The encoded line.</param>
        public void Append(string run, string line)
        {
            if (!_open.TryGetValue(run, out FileStream stream))
                return;

            try
            {
                byte[] bytes = _utf8.GetBytes(line);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
            catch (IOException exception)
            {
                Fail(run, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                Fail(run, exception);
            }
        }

        /// <summary>
        /// Releases the run's file.
        /// </summary>
        /// <param name="run">The run identity.</param>
        public void Close(string run)
        {
            if (!_open.TryGetValue(run, out FileStream stream))
                return;

            _open.Remove(run);
            try
            {
                stream.Dispose();
            }
            catch (IOException exception)
            {
                Fail(run, exception);
            }
        }

        /// <summary>
        /// Lists the run identities in the folder.
        /// </summary>
        /// <param name="cancellationToken">Unused; the listing is synchronous.</param>
        /// <returns>The identities, sorted, without the extension; empty when the folder is absent.</returns>
        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
        {
            List<string> runs = new();
            if (Directory.Exists(_root))
            {
                foreach (string path in Directory.GetFiles(_root, $"*{Extension}"))
                    runs.Add(Path.GetFileNameWithoutExtension(path));

                runs.Sort(StringComparer.Ordinal);
            }

            return Task.FromResult<IReadOnlyList<string>>(runs);
        }

        /// <summary>
        /// Reads one run's text, even while the run is open for writing.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>The text, or null when the run is absent.</returns>
        public async Task<string> ReadAsync(string run, CancellationToken cancellationToken)
        {
            string path = PathOf(run);
            if (!File.Exists(path))
                return null;

            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new(stream, _utf8);
            cancellationToken.ThrowIfCancellationRequested();
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Writes an exported run, creating its folder.
        /// </summary>
        /// <param name="destinationPath">The destination file path.</param>
        /// <param name="text">The exported text.</param>
        /// <param name="cancellationToken">Cancels the write.</param>
        /// <returns>A task that completes when the file is written.</returns>
        public async Task WriteExportAsync(string destinationPath, string text, CancellationToken cancellationToken)
        {
            string directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(destinationPath, text, _utf8, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes one run's file and nothing else.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="cancellationToken">Unused; the deletion is synchronous.</param>
        /// <returns>Whether a file was deleted.</returns>
        public Task<bool> DeleteAsync(string run, CancellationToken cancellationToken)
        {
            string path = PathOf(run);
            if (!File.Exists(path))
                return Task.FromResult(false);

            File.Delete(path);
            return Task.FromResult(true);
        }

        /// <summary>
        /// Records a run's I/O failure: one Warning for the run, its file released, its later lines dropped.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="exception">The failure.</param>
        private void Fail(string run, Exception exception)
        {
            if (_open.TryGetValue(run, out FileStream stream))
            {
                _open.Remove(run);
                try
                {
                    stream.Dispose();
                }
                catch (IOException)
                {
                    // The run is already failed; a second fault releasing it changes nothing.
                }
            }

            if (_failed.Add(run))
                _logger.Warning("Telemetry run could not be written; its later lines are dropped", LogField.Of("run", run), LogField.Of("error", exception.GetType().Name));
        }

        /// <summary>
        /// The file path of a run.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <returns>The path.</returns>
        private string PathOf(string run)
        {
            return Path.Combine(_root, $"{run}{Extension}");
        }
    }
}