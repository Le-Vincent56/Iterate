namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One transaction kind's count of identities issued so far. The pure-data form of an economy's
    /// per-kind counter, so a Session can be written and read back without reissuing an identity a
    /// transaction already carries.
    /// </summary>
    /// <param name="Kind">The transaction kind.</param>
    /// <param name="Count">How many identities that kind has issued; at least one.</param>
    public sealed record TransactionCounter(TransactionKind Kind, int Count);
}