using System;
using Iterate.Domain.Compilation;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The pure host-validity check run before a locked-target added execution begins.
    /// At this content exactly one clause is constructible from the runtime model — the locked
    /// position still holds the same instance — so this validator implements only that check
    /// (<c>HOST_MISSING</c>) and owns the cancellation-reason composition
    /// (<c>LOCKED_HOST_INVALID:&lt;token&gt;</c>): the validator, not its caller, decides the reason
    /// vocabulary. The remaining
    /// CAB-EVT-430 clauses are guaranteed structurally rather than implemented as dead branches: a
    /// locked host always carries a non-null executable definition because
    /// <see cref="Iterate.Domain.Content.InstructionInstance"/> construction requires one, and the
    /// arrangement is immutable for the whole execution, so a host present at lock time is present and
    /// unmoved at the resolution boundary. The check can only fail once a later child makes a host
    /// deletion, destruction, or retained-context invalidation representable, at which point it gains
    /// the tokens those failures name; until then the cancel site it feeds is end-to-end unreachable.
    /// </summary>
    public static class LockedHostValidator
    {
        /// <summary>
        /// The one constructible check token: the locked position no longer holds the locked instance.
        /// </summary>
        private const string HostMissing = "HOST_MISSING";

        /// <summary>
        /// The cancellation-reason prefix the validator owns.
        /// </summary>
        private const string ReasonPrefix = "LOCKED_HOST_INVALID:";

        /// <summary>
        /// Validates a lock against the current arrangement, returning null when the locked position
        /// still holds the same instance and <c>HOST_MISSING</c> when it does not — the instance has
        /// been removed from the arrangement or no longer occupies its locked position. The creator's
        /// original trigger is not re-evaluated.
        /// </summary>
        /// <param name="lock">The committed target lock.</param>
        /// <param name="arrangement">The compiled source arrangement being executed.</param>
        /// <returns>Null when the host is valid; the check token otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when the lock or arrangement is null.</exception>
        public static string Validate(TargetLock @lock, SourceArrangement arrangement)
        {
            if (@lock == null)
                throw new ArgumentException("Validating a locked host requires a lock.", nameof(@lock));

            if (arrangement == null)
                throw new ArgumentException("Validating a locked host requires an arrangement.", nameof(arrangement));

            if (!arrangement.TryFindInstance(@lock.HostInstance, out SourceSlot current)
                || current.Position != @lock.LockedSlot.Position)
            {
                return HostMissing;
            }

            return null;
        }

        /// <summary>
        /// Composes the cancellation reason for a failed check token.
        /// </summary>
        /// <param name="checkToken">The check token <see cref="Validate"/> returned.</param>
        /// <returns>The composed cancellation reason.</returns>
        public static string ComposeReason(string checkToken)
        {
            return ReasonPrefix + checkToken;
        }
    }
}