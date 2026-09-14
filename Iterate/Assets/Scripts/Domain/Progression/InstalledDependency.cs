using Iterate.Domain.Content;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One Dependency occupying RAM, with the price it was bought at. The price is kept on the
    /// installation rather than on the definition because it belongs to the offer that sold it, and a
    /// destruction refund is computed from what was actually paid.
    /// </summary>
    /// <param name="Instance">The installed Dependency instance.</param>
    /// <param name="PricePaid">The Tokens paid for it; zero for a reward.</param>
    /// <param name="Origin">How it was acquired.</param>
    public sealed record InstalledDependency(DependencyInstance Instance, int PricePaid, DependencyOrigin Origin);
}