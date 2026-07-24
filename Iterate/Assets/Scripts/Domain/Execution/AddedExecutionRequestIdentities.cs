using System;
using System.Globalization;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The engine-derived deterministic request-identity format: the creator's frequency-ledger key
    /// and the triggering event's ordinal, so one creator qualifying at one event always names the
    /// same request across replays.
    /// </summary>
    public static class AddedExecutionRequestIdentities
    {
        /// <summary>
        /// Composes the request identity for one creator qualifying at one triggering event.
        /// </summary>
        /// <param name="creator">The committed creator effect.</param>
        /// <param name="triggeringEvent">The event whose qualification created the request.</param>
        /// <returns>The identity, formatted as the frequency key, '@e', and the event ordinal.</returns>
        /// <exception cref="ArgumentException">Thrown when the creator is null.</exception>
        public static string For(ActiveEffect creator, TraceEventID triggeringEvent)
        {
            if (creator == null)
                throw new ArgumentException("A request identity requires a creator effect.", nameof(creator));

            return creator.FrequencyKey + "@e" + triggeringEvent.Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}