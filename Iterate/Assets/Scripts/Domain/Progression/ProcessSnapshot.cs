using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// An immutable view of a Process at one moment: the arrangement it opened with, its Buffer slots,
    /// its Bytes and its execution counts. Taken on demand; never stored, so it cannot go stale.
    /// </summary>
    /// <param name="InitialArrangement">The arrangement the Process opened with.</param>
    /// <param name="BufferSlots">The Buffer's slots at this moment.</param>
    /// <param name="Bytes">The Bytes available at this moment.</param>
    /// <param name="ExecutionsRun">How many executions have run.</param>
    /// <param name="ExecutionsRemaining">How many the allowance still permits.</param>
    /// <param name="IsOverflowing">Whether an item is held outside the Buffer.</param>
    public sealed record ProcessSnapshot(
        SourceArrangement InitialArrangement,
        IReadOnlyList<BufferSlot> BufferSlots,
        ByteAmount Bytes,
        int ExecutionsRun,
        int ExecutionsRemaining,
        bool IsOverflowing
    );
}