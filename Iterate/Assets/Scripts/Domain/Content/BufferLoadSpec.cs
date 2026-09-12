using System.Collections.Generic;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A Process's Instruction Buffer load plan. A scripted plan uses <see cref="Initial"/> and
    /// <see cref="Arrivals"/>; a drawn plan uses <see cref="InitialCount"/> and
    /// <see cref="ArrivalsAfterExecutions"/> over the confirmed Active Branch. The unused pair is
    /// empty rather than null, and the rule set rejects a plan that fills the wrong pair.
    /// </summary>
    /// <param name="Policy">Whether the load is scripted or drawn.</param>
    /// <param name="Initial">The scripted starting Buffer content, in authored order.</param>
    /// <param name="Arrivals">The scripted arrival moments, in authored order.</param>
    /// <param name="InitialCount">The number of items drawn into the Buffer at Process start.</param>
    /// <param name="ArrivalsAfterExecutions">The executions after which a drawn arrival occurs.</param>
    public sealed record BufferLoadSpec(
        BufferLoadPolicy Policy,
        IReadOnlyList<string> Initial,
        IReadOnlyList<ArrivalLoadSpec> Arrivals,
        int InitialCount,
        IReadOnlyList<int> ArrivalsAfterExecutions
    );
}