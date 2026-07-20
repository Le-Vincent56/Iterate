using System.Collections.Generic;
using Iterate.Domain.Content;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The immutable lock snapshot a successful commit produces: the arrangement, the
    /// activation-ordered consumed pragmas, and the full cost record. Distinct from the uncommitted Build
    /// arrangement, the Repository, the Buffer, and any serialized asset.
    /// </summary>
    /// <param name="Arrangement">The committed source arrangement.</param>
    /// <param name="Pragmas">The activation-ordered Directive pragmas consumed into this compilation.</param>
    /// <param name="CostRecord">The cost breakdown resolved at commitment.</param>
    public sealed record CompiledSource(
        SourceArrangement Arrangement,
        IReadOnlyList<DirectiveInstance> Pragmas,
        CompilationCostBreakdown CostRecord
    );
}