using System;
using Iterate.Application.Telemetry;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// A telemetry clock the test advances by hand: both its UTC time and its monotonic count move
    /// together, from a fixed start.
    /// </summary>
    public sealed class FakeTelemetryClock : ITelemetryClock
    {
        /// <summary>
        /// The current UTC time.
        /// </summary>
        public DateTimeOffset UtcNow { get; private set; } = new DateTimeOffset(new DateTime(2026, 11, 24), TimeSpan.Zero).AddHours(12);

        /// <summary>
        /// The monotonic milliseconds advanced so far.
        /// </summary>
        public long ElapsedMilliseconds { get; private set; }

        /// <summary>
        /// Advances both readings.
        /// </summary>
        /// <param name="milliseconds">The milliseconds to advance.</param>
        public void Advance(long milliseconds)
        {
            ElapsedMilliseconds += milliseconds;
            UtcNow = UtcNow.AddMilliseconds(milliseconds);
        }
    }
}
