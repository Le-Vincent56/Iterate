using System.Collections.Generic;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// One Repository entry as a checkpoint holds it: identities and the definition to resolve content
    /// from, rather than the content itself. The acquisition suffix is carried because it is the number
    /// the player learned for the item and must survive a save unchanged.
    /// </summary>
    /// <param name="Instance">The item instance's identity.</param>
    /// <param name="Definition">The content definition's stable identity.</param>
    /// <param name="Origin">How the item was acquired.</param>
    /// <param name="StarterProtected">Whether the entry is a starter and cannot be deleted.</param>
    /// <param name="Suffix">The per-definition acquisition suffix.</param>
    /// <param name="Attachments">The Patches attached to the item, in socket order.</param>
    public sealed record CheckpointRepositoryItem(
        InstanceID Instance,
        string Definition,
        AcquisitionOrigin Origin,
        bool StarterProtected,
        int Suffix,
        IReadOnlyList<CheckpointAttachment> Attachments
    );
}