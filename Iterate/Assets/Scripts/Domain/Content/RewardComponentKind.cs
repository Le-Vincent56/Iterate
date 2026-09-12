namespace Iterate.Domain.Content
{
    /// <summary>
    /// The closed set of reward-component kinds. Token-bearing kinds carry an amount; content-bearing
    /// kinds carry a reference. Components stay separate rather than summed, so a Token award, a
    /// route bonus and a Benchmark bonus remain distinguishable. Serialized in JSON as TOKENS,
    /// POOL_CHOICE, GUARANTEED_CONTENT, CACHED_PATCH, PATCH, ROUTE_TOKEN_BONUS, RAM_SET.
    /// </summary>
    public enum RewardComponentKind
    {
        Tokens,
        PoolChoice,
        GuaranteedContent,
        CachedPatch,
        Patch,
        RouteTokenBonus,
        RAMSet
    }
}