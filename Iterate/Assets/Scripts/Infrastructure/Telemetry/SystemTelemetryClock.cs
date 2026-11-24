using System;
using System.Diagnostics;
using Iterate.Application.Telemetry;

namespace Iterate.Infrastructure.Telemetry
{
    /// <summary>
    /// The system clock for telemetry: UTC wall time to stamp lines, and a stopwatch started at
    /// construction to measure durations monotonically.
    /// </summary>
    public sealed class SystemTelemetryClock : ITelemetryClock
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        /// <summary>
        /// The current UTC time.
        /// </summary>
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        /// <summary>
        /// Milliseconds since construction.
        /// </summary>
        public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;
    }
}