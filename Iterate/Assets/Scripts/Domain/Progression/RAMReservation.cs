using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// RAM reserved by an installation.
    /// </summary>
    /// <param name="Dependency">The Dependency instance that reserved it.</param>
    /// <param name="Amount">The RAM reserved.</param>
    /// <param name="UsageAfter">The rack usage after the reservation.</param>
    public sealed record RAMReservation(InstanceID Dependency, int Amount, int UsageAfter) : TransactionConsequence;
}