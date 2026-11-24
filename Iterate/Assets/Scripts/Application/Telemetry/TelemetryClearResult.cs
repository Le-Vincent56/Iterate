using System.Collections.Generic;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// What a clear did (UX-VAL-028): the runs deleted, and the runs refused because they are
    /// active or were not on disk.
    /// </summary>
    /// <param name="Deleted">The runs deleted.</param>
    /// <param name="Refused">The runs not deleted.</param>
    public sealed record TelemetryClearResult(IReadOnlyList<string> Deleted, IReadOnlyList<string> Refused);
}