using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// One route the choice offers: where it leads and which shop comes first.
    /// </summary>
    /// <param name="Route">The route.</param>
    /// <param name="DisplayName">Its display name.</param>
    /// <param name="Process">The Process it leads to.</param>
    /// <param name="Shop">The shop before that Process.</param>
    public sealed record RouteOptionView(
        RouteID Route,
        string DisplayName,
        ProcessID Process,
        ShopID Shop
    );
}