using Iterate.Domain.Execution;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// One member of the F1-F5 fixture register: the name it is known by on both sides of the twin, and
    /// the request that produces its record.
    /// </summary>
    /// <param name="Name">The fixture's registered name.</param>
    /// <param name="Request">The request that produces the fixture's record.</param>
    internal sealed record ShippedCatalogFixture(string Name, ExecutionRequest Request);
}
