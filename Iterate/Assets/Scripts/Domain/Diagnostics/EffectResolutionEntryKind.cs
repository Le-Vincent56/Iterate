namespace Iterate.Domain.Diagnostics
{
    /// <summary>
    /// What one effect origin did at one boundary: its declared requirements succeeded, it committed to
    /// resolution under its frequency rule, or it observed the candidate and did not qualify. The kind
    /// is read from the event's subtype rather than its disposition, because the engine records a null
    /// disposition on the qualified and committed events and a disposition only on the near-miss.
    /// </summary>
    public enum EffectResolutionEntryKind
    {
        /// <summary>
        /// The effect's declared requirements succeeded.
        /// </summary>
        Qualified,

        /// <summary>
        /// The effect committed to resolution under its frequency rule.
        /// </summary>
        Committed,

        /// <summary>
        /// The effect observed the candidate and did not satisfy its requirements.
        /// </summary>
        FailedToQualify
    }
}