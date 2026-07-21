using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One Score-band crossing produced by a finalized Score change: the canonical band name, the band's
    /// Score value, and the CAB-verbatim crossing-direction subtype token. Carries no consequence — bands
    /// are applied elsewhere.
    /// </summary>
    /// <param name="ThresholdName">The canonical band name crossed.</param>
    /// <param name="Threshold">The band's Score value.</param>
    /// <param name="Subtype">The crossing-direction subtype token.</param>
    public sealed record ThresholdCrossing(string ThresholdName, ScoreValue Threshold, string Subtype);
}