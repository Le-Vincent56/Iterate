using System.Collections.Generic;
using Iterate.Domain.Compilation;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The source inside the Core and its compilation status, including the cost the next compilation
    /// would pay. Every list is a copy.
    /// </summary>
    /// <param name="Slots">The source lines, in order.</param>
    /// <param name="Status">The compilation status and cost preview, computed when the view was taken.</param>
    /// <param name="PendingDirectives">How many Directives are activated for the next compilation.</param>
    public sealed record BuildView(IReadOnlyList<SourceSlotView> Slots, BuildStatus Status, int PendingDirectives);
}