using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen acquisition-pool definition: the selection method, the count a player-choice pool
    /// offers, and the membership. The selection method is a controlled-vocabulary string rather than
    /// an enum so a new admitted method is a data edit; membership in the controlled set is the
    /// validator's check, and this record only requires the method to be stated. Not a content item.
    /// </summary>
    /// <param name="ID">The pool's surrogate-key identity.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="SelectionMethod">The controlled selection-method token.</param>
    /// <param name="SelectionCount">The number a player-choice pool offers; null for a reroll pool.</param>
    /// <param name="Members">The pool membership, in authored order.</param>
    public sealed record PoolDefinition(
        PoolID ID,
        string DisplayName,
        string SelectionMethod,
        int? SelectionCount,
        IReadOnlyList<PoolMember> Members
    )
    {
        /// <summary>
        /// The controlled selection-method token. Validated non-empty at construction; membership in
        /// the controlled vocabulary is the validator's check.
        /// </summary>
        public string SelectionMethod { get; } = RequireSelectionMethod(SelectionMethod);

        /// <summary>
        /// Validates that a selection method is stated.
        /// </summary>
        /// <param name="selectionMethod">The candidate selection-method token.</param>
        /// <returns>The token unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the token is null or empty.</exception>
        private static string RequireSelectionMethod(string selectionMethod)
        {
            if (string.IsNullOrEmpty(selectionMethod))
                throw new ArgumentException("A PoolDefinition requires a selection method.", nameof(selectionMethod));

            return selectionMethod;
        }
    }
}