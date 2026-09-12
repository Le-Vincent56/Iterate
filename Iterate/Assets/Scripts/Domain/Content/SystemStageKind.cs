namespace Iterate.Domain.Content
{
    /// <summary>
    /// The closed set of stage kinds a System walks: a Process, a shop, or a choice between routes.
    /// Serialized in JSON as PROCESS, SHOP, ROUTE_SELECTION.
    /// </summary>
    public enum SystemStageKind
    {
        Process,
        Shop,
        RouteSelection
    }
}