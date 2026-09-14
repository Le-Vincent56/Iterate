using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A bought Utility bound to the one Process it will apply to. Scope is the exact target Process
    /// (spec section 6), so a commitment is addressed by Process identity rather than by "the next
    /// Process", which no longer means anything once a shop can be opened out of order.
    /// </summary>
    /// <param name="Identity">The commitment's own instance identity.</param>
    /// <param name="Definition">The frozen Utility definition.</param>
    /// <param name="TargetProcess">The Process this Utility applies to.</param>
    /// <param name="OfferID">The offer that sold it.</param>
    public sealed record UtilityCommitment(
        InstanceID Identity,
        UtilityDefinition Definition,
        ProcessID TargetProcess,
        string OfferID
    );
}