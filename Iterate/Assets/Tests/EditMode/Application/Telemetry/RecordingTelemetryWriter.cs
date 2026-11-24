using System;
using System.Collections.Generic;
using Iterate.Application.Telemetry;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// A telemetry writer that keeps every line in memory, records the order of opens, appends and
    /// closes, and can be told to throw once, on the first line containing a given text, to drive
    /// the recorder's fault barrier.
    /// </summary>
    public sealed class RecordingTelemetryWriter : ITelemetryWriter
    {
        /// <summary>
        /// Each run's lines, in append order.
        /// </summary>
        public Dictionary<string, List<string>> Lines { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Every open, append and close, in order, as <c>open run</c>, <c>append run</c> or <c>close run</c>.
        /// </summary>
        public List<string> Log { get; } = new();

        /// <summary>
        /// When set, the next line containing this text throws instead of being kept; then it clears.
        /// </summary>
        public string ThrowOnceOn { get; set; }

        /// <summary>
        /// Records an open.
        /// </summary>
        /// <param name="run">The run identity.</param>
        public void Open(string run)
        {
            Log.Add($"open {run}");
            if (!Lines.ContainsKey(run))
                Lines[run] = new List<string>();
        }

        /// <summary>
        /// Keeps a line, or throws once when it contains <see cref="ThrowOnceOn"/>.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="line">The encoded line.</param>
        public void Append(string run, string line)
        {
            if (ThrowOnceOn != null && line.Contains(ThrowOnceOn))
            {
                ThrowOnceOn = null;
                throw new InvalidOperationException("forced writer fault");
            }

            Log.Add($"append {run}");
            Lines[run].Add(line);
        }

        /// <summary>
        /// Records a close.
        /// </summary>
        /// <param name="run">The run identity.</param>
        public void Close(string run)
        {
            Log.Add($"close {run}");
        }

        /// <summary>
        /// A run's whole text.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <returns>Its lines joined.</returns>
        public string Text(string run)
        {
            return string.Concat(Lines[run]);
        }
    }
}
