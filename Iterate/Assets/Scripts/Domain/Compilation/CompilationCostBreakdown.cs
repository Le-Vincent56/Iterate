using System.Collections.Generic;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The full cost record: classification, progression index, whether a base
    /// cost was defined and its value, the ordered modifier entries, the final payable cost, and whether the
    /// compilation advances progression. A single "Bytes spent" field is explicitly insufficient.
    /// </summary>
    /// <param name="Classification">The compilation classification.</param>
    /// <param name="ProgressionIndex">The progression index; zero unless ordinary edited.</param>
    /// <param name="BaseCostDefined">Whether a base cost is defined for this classification and index.</param>
    /// <param name="BaseCost">The base cost before modifiers; zero when undefined.</param>
    /// <param name="Modifiers">The ordered cost-modifier entries applied.</param>
    /// <param name="FinalCost">The final payable cost in Bytes.</param>
    /// <param name="AdvancesProgression">Whether the compilation advances edited-compilation progression.</param>
    public sealed record CompilationCostBreakdown(
        CompilationClassification Classification,
        int ProgressionIndex,
        bool BaseCostDefined,
        int BaseCost,
        IReadOnlyList<CostModifierEntry> Modifiers,
        int FinalCost,
        bool AdvancesProgression
    );
}