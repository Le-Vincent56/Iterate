using Iterate.Domain.Trace;

namespace Iterate.Application.Diagnostics
{
    /// <summary>
    /// The outcome of reading a generated execution record.
    /// </summary>
    /// <param name="Outcome">How the read ended.</param>
    /// <param name="Record">The rebuilt record, or null.</param>
    /// <param name="FormatVersion">The version found, or zero.</param>
    /// <param name="Defect">A sentence naming what was wrong, or null.</param>
    public sealed record ExecutionRecordReadResult(
        ExecutionRecordReadOutcome Outcome,
        ExecutionRecord Record,
        int FormatVersion,
        string Defect
    );
}