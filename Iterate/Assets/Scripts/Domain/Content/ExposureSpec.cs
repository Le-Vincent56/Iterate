using System.Collections.Generic;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A Process's exposure plan: the content guaranteed to arrive before any drawn arrival. Present
    /// only on a drawn Buffer load.
    /// </summary>
    /// <param name="Guaranteed">The content IDs guaranteed to arrive, in authored order.</param>
    public sealed record ExposureSpec(IReadOnlyList<string> Guaranteed);
}