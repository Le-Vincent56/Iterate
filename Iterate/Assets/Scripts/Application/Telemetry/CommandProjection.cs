using Iterate.Application.Content.Json;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// A command's telemetry target and data, by data identity.
    /// </summary>
    /// <param name="Target">The target, or null.</param>
    /// <param name="Data">The command's arguments and what changed.</param>
    public readonly record struct CommandProjection(string Target, JsonObject Data);
}