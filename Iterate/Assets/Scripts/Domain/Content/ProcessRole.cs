namespace Iterate.Domain.Content
{
    /// <summary>
    /// The closed set of roles a System 1 Process configuration declares. Serialized in JSON as
    /// TUTORIAL_1, TUTORIAL_2, ROUTE_PROCESS_3, CRITICAL_PROCESS.
    /// </summary>
    public enum ProcessRole
    {
        Tutorial1,
        Tutorial2,
        RouteProcess3,
        CriticalProcess
    }
}