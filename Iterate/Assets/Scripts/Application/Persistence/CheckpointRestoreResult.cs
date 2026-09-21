using Iterate.Domain.Progression;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The outcome of restoring a checkpoint. A rejection carries neither a Session nor a destination.
    /// When the Session itself refused the rebuilt state, its own rejection is carried through rather
    /// than flattened, so a caller can tell a content problem from a state problem.
    /// </summary>
    /// <param name="Succeeded">Whether the checkpoint restored.</param>
    /// <param name="Session">The restored Session, or null.</param>
    /// <param name="Destination">Where the Session resumes, or null.</param>
    /// <param name="Rejection">Why the checkpoint was refused, or None.</param>
    /// <param name="SessionRejection">The Session's own rejection when it refused the state, or None.</param>
    /// <param name="Detail">A sentence naming what was wrong, or null.</param>
    public sealed record CheckpointRestoreResult(
        bool Succeeded,
        SessionState Session,
        CheckpointDestination Destination,
        CheckpointRestoreRejection Rejection,
        SessionRestoreRejection SessionRejection,
        string Detail
    );
}