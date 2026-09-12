using System;
using System.Collections.Generic;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of firing one arrival moment: the items that arrived, in authored order, or the
    /// typed reason nothing did. A rejected moment admits nothing at all — a moment is all-or-nothing,
    /// so a load naming one unavailable item never half-arrives.
    /// </summary>
    /// <param name="Succeeded">Whether the moment fired.</param>
    /// <param name="Rejection">The rejection reason; None on success.</param>
    /// <param name="Arrived">The items that arrived, in authored order.</param>
    public readonly record struct ArrivalResult(
        bool Succeeded,
        ArrivalRejection Rejection,
        IReadOnlyList<RepositoryItem> Arrived
    )
    {
        /// <summary>
        /// Creates a successful result carrying the arrived items.
        /// </summary>
        /// <param name="arrived">The items that arrived, in authored order.</param>
        /// <returns>The arrival result.</returns>
        public static ArrivalResult Success(IReadOnlyList<RepositoryItem> arrived)
        {
            return new ArrivalResult(true, ArrivalRejection.None, arrived);
        }

        /// <summary>
        /// Creates a rejected result carrying a reason.
        /// </summary>
        /// <param name="rejection">The rejection reason.</param>
        /// <returns>The arrival result.</returns>
        public static ArrivalResult Rejected(ArrivalRejection rejection)
        {
            return new ArrivalResult(false, rejection, Array.Empty<RepositoryItem>());
        }
    }
}