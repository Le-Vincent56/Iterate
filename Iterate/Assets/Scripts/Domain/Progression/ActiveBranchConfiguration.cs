using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A confirmed Active Branch: the locked set of instances eligible to reach a Process. Unordered —
    /// Branch order carries no mechanical meaning — and immutable, so a later deletion from the
    /// Repository never rewrites what was confirmed.
    /// </summary>
    /// <param name="Eligible">The confirmed eligible instances.</param>
    public sealed record ActiveBranchConfiguration(IReadOnlyList<InstanceID> Eligible);
}