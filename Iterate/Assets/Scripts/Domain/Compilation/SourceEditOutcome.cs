using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The successful product of an edit: the new arrangement plus any instances archived as a
    /// consequence. For every ordinary edit the archive is empty; Overwrite populates it later.
    /// </summary>
    /// <param name="Arrangement">The arrangement produced by the edit.</param>
    /// <param name="ArchivedInstances">The instances archived as a consequence; empty for ordinary edits.</param>
    public sealed record SourceEditOutcome(SourceArrangement Arrangement, IReadOnlyList<InstanceID> ArchivedInstances);
}