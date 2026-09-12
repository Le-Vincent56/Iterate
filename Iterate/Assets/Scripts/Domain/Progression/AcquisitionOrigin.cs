namespace Iterate.Domain.Progression
{
    /// <summary>
    /// How a Repository entry was acquired. Starter entries are protected from deletion for the whole
    /// Session; every other origin is deletable.
    /// </summary>
    public enum AcquisitionOrigin
    {
        Starter,
        Reward,
        Purchase,
        Duplicate
    }
}