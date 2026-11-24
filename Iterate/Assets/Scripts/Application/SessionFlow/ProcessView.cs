using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The Process in play. Pending arrivals are a count only: the identities of what will arrive are
    /// not shown before it arrives. Every list is a copy.
    /// </summary>
    /// <param name="ProcessIdentity">The Process.</param>
    /// <param name="Slots">The Buffer slots, in order.</param>
    /// <param name="Incoming">The item holding the Buffer in overflow, or null.</param>
    /// <param name="PendingArrivals">How many arrivals are still queued.</param>
    /// <param name="Archived">The items archived this Process.</param>
    /// <param name="Consumed">The items consumed this Process.</param>
    /// <param name="Exposed">The items exposed this Process, in arrival order.</param>
    /// <param name="Bytes">The Byte balance.</param>
    /// <param name="ExecutionsRun">How many executions have run.</param>
    /// <param name="ExecutionsRemaining">How many executions remain.</param>
    /// <param name="MandatoryExecutionsRemaining">How many executions are still owed.</param>
    public sealed record ProcessView(
        string ProcessIdentity,
        IReadOnlyList<BufferSlotView> Slots,
        InstanceID? Incoming,
        int PendingArrivals,
        IReadOnlyList<InstanceID> Archived,
        IReadOnlyList<InstanceID> Consumed,
        IReadOnlyList<InstanceID> Exposed,
        int Bytes,
        int ExecutionsRun,
        int ExecutionsRemaining,
        int MandatoryExecutionsRemaining
    );
}