namespace Iterate.Domain.Content
{
    /// <summary>
    /// Whether an effect's timing names a controlled causal band or a controlled named scheduling
    /// boundary. Serialized in JSON as BAND, NAMED_BOUNDARY.
    /// </summary>
    public enum TimingKind
    {
        Band,
        NamedBoundary
    }
}