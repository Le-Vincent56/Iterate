using System;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// One safety ceiling that was breached: the limit's name, its ceiling value, and the count at the
    /// moment of breach. The count may equal or exceed the ceiling; a ceiling is always one or greater.
    /// </summary>
    public sealed record BreachedLimit
    {
        /// <summary>
        /// The name of the breached limit; never empty.
        /// </summary>
        public string LimitName { get; }

        /// <summary>
        /// The ceiling value of the breached limit; always one or greater.
        /// </summary>
        public int Ceiling { get; }

        /// <summary>
        /// The count at the moment of breach; always zero or greater.
        /// </summary>
        public int CountAtBreach { get; }

        public BreachedLimit(string limitName, int ceiling, int countAtBreach)
        {
            if (string.IsNullOrEmpty(limitName))
                throw new ArgumentException("A breached limit requires a name.", nameof(limitName));
            
            if (ceiling < 1)
                throw new ArgumentException("A breached limit requires a ceiling of at least one.", nameof(ceiling));
            
            if (countAtBreach < 0)
                throw new ArgumentException("A breached-limit count may not be negative.", nameof(countAtBreach));

            LimitName = limitName;
            Ceiling = ceiling;
            CountAtBreach = countAtBreach;
        }
    }
}