namespace Iterate.Domain.Content
{
    /// <summary>
    /// The controlled event-family registry an effect trigger observes: the 15 families of the CAB
    /// event registry, orthogonal to content category. Serialized in JSON as the uppercase family
    /// tokens (LIFECYCLE, SOURCE, …, SAFETY).
    /// </summary>
    public enum EventFamily
    {
        Lifecycle,
        Source,
        Operation,
        Qualification,
        Quantity,
        Structure,
        Disposition,
        Reaction,
        AddedExecution,
        Intervention,
        Threshold,
        Transaction,
        ContentLifecycle,
        RandomSelection,
        Safety
    }
}