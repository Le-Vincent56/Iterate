using Iterate.Application.Persistence;

namespace Iterate.Application.Onboarding
{
    /// <summary>
    /// What the launch service learned from the save files: the decision, and, when onboarding can
    /// resume, the checkpoint and its restored Session.
    /// </summary>
    /// <param name="Decision">The launch decision.</param>
    /// <param name="Checkpoint">The checkpoint to resume from; null unless resuming.</param>
    /// <param name="Restored">The checkpoint's restored Session and destination; null unless resuming.</param>
    /// <param name="CheckpointPresent">Whether a checkpoint file exists, usable or not.</param>
    internal sealed record LaunchPlan(
        LaunchDecision Decision,
        CheckpointState Checkpoint,
        CheckpointRestoreResult Restored,
        bool CheckpointPresent
    );
}