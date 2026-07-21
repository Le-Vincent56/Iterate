namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The closed base of the family-typed event payload slot. An event record carries at most one
    /// payload; its concrete type identifies the grounded evidence a family contributes. Derivation is
    /// closed to this assembly through a non-public constructor, so the payload set extends only by
    /// additively declaring new Domain payloads — never by outside code.
    /// </summary>
    public abstract record EventPayload
    {
        private protected EventPayload() { }
    }
}