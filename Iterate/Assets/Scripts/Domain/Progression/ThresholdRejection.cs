namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Why a threshold evaluation refused to name a tier. An execution that could not complete
    /// produces a final output like any other, so refusing is what keeps it from being rewarded.
    /// </summary>
    public enum ThresholdRejection
    {
        None,
        ResultInvalid
    }
}