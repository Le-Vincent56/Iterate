namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The outcome of capturing a Session. A rejection carries no checkpoint at all.
    /// </summary>
    /// <param name="Succeeded">Whether the Session was captured.</param>
    /// <param name="State">The captured checkpoint, or null.</param>
    /// <param name="Rejection">Why the capture was refused, or None.</param>
    public sealed record CheckpointCaptureResult(
        bool Succeeded,
        CheckpointState State,
        CheckpointCaptureRejection Rejection
    );
}