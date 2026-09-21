namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// One (selection purpose, governing context) pair's highest issued occurrence ordinal. The
    /// pure-data form of a tracker entry, so a Session's ordinals can be written and read back without
    /// the tracker exposing its keying.
    /// </summary>
    /// <param name="SelectionPurpose">The selection purpose; non-empty.</param>
    /// <param name="GoverningContextIdentity">The governing context scope identity; non-empty.</param>
    /// <param name="Count">The highest ordinal issued for the pair; at least one.</param>
    public sealed record OrdinalCounter(string SelectionPurpose, string GoverningContextIdentity, int Count);
}