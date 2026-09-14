using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// What destroying a Dependency would return: the RAM released and the refund, which is a floored
    /// half of what was actually paid, so a rewarded Dependency refunds nothing.
    /// </summary>
    /// <param name="Rejection">Why it would be refused; None when it would succeed.</param>
    /// <param name="RAMReleased">The usage that would be released.</param>
    /// <param name="PricePaid">What was paid for it.</param>
    /// <param name="Refund">The Tokens that would be refunded.</param>
    /// <param name="TokensAfter">The balance after, when it would succeed.</param>
    public sealed record DependencyDestructionPreview(
        DependencyRejection Rejection,
        int RAMReleased,
        int PricePaid,
        int Refund,
        TokenAmount TokensAfter
    );
}