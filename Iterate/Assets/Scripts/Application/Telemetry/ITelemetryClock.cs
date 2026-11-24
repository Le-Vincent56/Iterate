using System;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Times telemetry: wall-clock UTC to stamp lines, and a monotonic count to measure durations,
    /// so a wall-clock change never produces a negative dwell.
    /// </summary>
    public interface ITelemetryClock
    {
        /// <summary>
        /// The current UTC time.
        /// </summary>
        DateTimeOffset UtcNow { get; }

        /// <summary>
        /// Monotonic milliseconds since an arbitrary fixed start.
        /// </summary>
        long ElapsedMilliseconds { get; }
    }
}