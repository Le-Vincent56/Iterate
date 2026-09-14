using System.Collections.Generic;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of a reroll, carrying the Determinism record that chose the replacements so a
    /// replay can be compared against it.
    /// </summary>
    /// <param name="Succeeded">Whether the reroll happened.</param>
    /// <param name="Rejection">Why it was refused; None on success.</param>
    /// <param name="Record">The committed transaction; null on refusal.</param>
    /// <param name="Decision">The decision record; null on refusal.</param>
    /// <param name="ReplacedSlots">The slot numbers replaced, ascending.</param>
    public sealed record RerollResult(
        bool Succeeded,
        ShopRejection Rejection,
        TransactionRecord Record,
        RandomDecisionRecord Decision,
        IReadOnlyList<int> ReplacedSlots
    );
}