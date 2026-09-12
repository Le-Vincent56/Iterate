using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen Process configuration: everything a Process needs to open, authored as data rather
    /// than code. Not a content item — a Process configuration carries no rules text, category,
    /// rarity or tags. The cross-record shape rules (thresholds ascending, executions at least one,
    /// exposure only with a drawn load, drawn counts inside Branch capacity) are the catalog
    /// validator's, not this record's.
    /// </summary>
    /// <param name="ID">The Process's surrogate-key identity.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Role">The Process's role in the System.</param>
    /// <param name="Core">The Core this Process executes.</param>
    /// <param name="ProcessRule">The Process rule this Process carries; null when it carries none.</param>
    /// <param name="Thresholds">The three authored success thresholds.</param>
    /// <param name="Executions">The number of executions the Process allows.</param>
    /// <param name="MandatoryExecutions">Whether every allowed execution must be run.</param>
    /// <param name="StartingBytes">The Bytes the Process starts with.</param>
    /// <param name="BufferCapacity">The Instruction Buffer's slot count.</param>
    /// <param name="SourceCapacity">The number of open Core positions, cross-checked against the Core.</param>
    /// <param name="InitialSource">The content pre-installed in the source, in authored order.</param>
    /// <param name="BufferLoad">The Instruction Buffer load plan.</param>
    /// <param name="Exposure">The exposure plan; null on a scripted load.</param>
    /// <param name="ActiveBranch">The Active Branch constraints; null on a tutorial Process.</param>
    /// <param name="RewardPackage">The reward package this Process awards.</param>
    /// <param name="PrecedingShop">The shop opening before this Process; null when none does.</param>
    public sealed record ProcessConfigurationDefinition(
        ProcessID ID,
        string DisplayName,
        ProcessRole Role,
        CoreID Core,
        ProcessRuleID? ProcessRule,
        ProcessThresholdSpec Thresholds,
        int Executions,
        bool MandatoryExecutions,
        int StartingBytes,
        int BufferCapacity,
        int SourceCapacity,
        IReadOnlyList<InitialSourceSpec> InitialSource,
        BufferLoadSpec BufferLoad,
        ExposureSpec Exposure,
        ActiveBranchSpec ActiveBranch,
        RewardPackageID RewardPackage,
        ShopID? PrecedingShop
    );
}