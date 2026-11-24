namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Marks a player intent raised against the Session's command surface. A command carries
    /// identities and primitives only, never a Domain object, so a presenter can build one from what
    /// it displays and telemetry can record it as it was raised.
    /// </summary>
    public interface ISessionCommand { }
}