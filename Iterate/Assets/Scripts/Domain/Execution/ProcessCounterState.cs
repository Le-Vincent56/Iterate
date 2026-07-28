using System;
using Iterate.Domain.Content;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The single Process counter a Process rule owns, initialized to zero at construction and reset
    /// to zero at execution expiration. Heat is the only Process counter the current content declares,
    /// so this is deliberately one counter rather than a named registry; a second counter is a later
    /// change made when a second counter exists.
    /// A request is never refused. When it would cross a bound the declared bound applies, the change
    /// is reduced to what the bound permits, and the commit reports both the requested and the applied
    /// delta so the difference stands as the bound evidence.
    /// </summary>
    public sealed class ProcessCounterState
    {
        /// <summary>
        /// The counter's current value; zero before any change and after a reset.
        /// </summary>
        public int Value { get; private set; }

        /// <summary>
        /// Applies one counter request, clamping to whichever bounds it declares.
        /// </summary>
        /// <param name="request">The counter request to apply.</param>
        /// <returns>The commit describing prior value, requested and applied delta, and final value.</returns>
        /// <exception cref="ArgumentException">Thrown when the request is null.</exception>
        public ProcessCounterCommit Apply(CounterRequestOperation request)
        {
            if (request == null)
                throw new ArgumentException("A counter change requires a request.", nameof(request));

            int prior = Value;
            int proposed = prior + request.Delta;

            if (request.HasCeiling && proposed > request.Ceiling)
                proposed = request.Ceiling;

            if (request.HasFloor && proposed < request.Floor)
                proposed = request.Floor;

            Value = proposed;
            int finalDelta = proposed - prior;

            return new ProcessCounterCommit(prior, request.Delta, finalDelta, proposed, finalDelta != request.Delta);
        }

        /// <summary>
        /// Returns the counter to zero — the execution-expiration reset. No value carries between
        /// executions.
        /// </summary>
        public void Reset()
        {
            Value = 0;
        }
    }
}