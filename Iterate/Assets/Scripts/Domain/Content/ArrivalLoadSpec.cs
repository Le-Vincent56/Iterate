using System.Collections.Generic;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// One scripted arrival moment: the execution it follows and the content that arrives, in
    /// authored order. Arrival order is the authored list order, so an overflow reproduces the same
    /// held item every run.
    /// </summary>
    /// <param name="AfterExecution">The execution number this moment follows.</param>
    /// <param name="Items">The content IDs arriving at this moment, in authored order.</param>
    public sealed record ArrivalLoadSpec(int AfterExecution, IReadOnlyList<string> Items);
}