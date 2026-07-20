using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The read-only projection of Build state for UI and Session Flow: the six independent
    /// states plus the transparency values (classification, cost preview, affordability, and
    /// the next edited-compilation base cost).
    /// </summary>
    /// <param name="SourceCompiled">Whether a compiled source exists and the current source matches it.</param>
    /// <param name="SourceChanged">Whether the current source differs from the compiled source.</param>
    /// <param name="OrdinaryEditsPresent">Whether ordinary edits are present.</param>
    /// <param name="FreeOnlyChangesPresent">Whether only free-only changes are present.</param>
    /// <param name="PendingDirectivePragma">Whether a Directive pragma is pending.</param>
    /// <param name="CompilationBlocked">Whether a commit would be blocked.</param>
    /// <param name="Classification">The current compilation classification.</param>
    /// <param name="Preview">The previewed cost breakdown.</param>
    /// <param name="AvailableBytes">The Bytes available.</param>
    /// <param name="NextEditedBaseCostDefined">Whether the next edited compilation has a defined base cost.</param>
    /// <param name="NextEditedBaseCost">The next edited-compilation base cost; zero when undefined.</param>
    public sealed record BuildStatus(
        bool SourceCompiled,
        bool SourceChanged,
        bool OrdinaryEditsPresent,
        bool FreeOnlyChangesPresent,
        bool PendingDirectivePragma,
        bool CompilationBlocked,
        CompilationClassification Classification,
        CompilationCostBreakdown Preview,
        ByteAmount AvailableBytes,
        bool NextEditedBaseCostDefined,
        int NextEditedBaseCost
    );
}