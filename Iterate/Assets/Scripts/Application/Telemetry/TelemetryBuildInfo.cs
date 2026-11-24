namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// The versions every telemetry line carries besides the catalog revision.
    /// </summary>
    /// <param name="BuildVersion">The build version.</param>
    /// <param name="UXVersion">The UX source version the build implements.</param>
    public sealed record TelemetryBuildInfo(string BuildVersion, string UXVersion);
}