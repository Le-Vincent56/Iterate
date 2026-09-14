namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Why a Token movement happened. Every ledger entry carries one, so a Session's Token history
    /// answers "what was this spent on" without a caller correlating records.
    /// </summary>
    public enum TokenBasis
    {
        RewardTokens,
        RouteBonus,
        BenchmarkBonus,
        OfferPrice,
        RerollCost,
        ServicePrice,
        ReplacementFee,
        DestructionRefund
    }
}