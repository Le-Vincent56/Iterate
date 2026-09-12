namespace Iterate.Domain.Progression
{
    /// <summary>
    /// What happened to an item offered to the Buffer: it took a slot, or the Buffer was full and it
    /// is being held outside in an overflow state.
    /// </summary>
    public enum AdmissionOutcome
    {
        Admitted,
        Held
    }
}