namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The typed reason an ordinary or Overwrite source edit was rejected — the game vocabulary
    /// minus execution-phase and tutorial gating, which Session Flow owns. Legal-play failures are these
    /// values, never exceptions.
    /// </summary>
    public enum SourceEditRejection
    {
        /// <summary>
        /// No rejection; the edit succeeded.
        /// </summary>
        None,

        /// <summary>
        /// The referenced position is outside the arrangement.
        /// </summary>
        PositionNotFound,

        /// <summary>
        /// The target is an immutable Core line.
        /// </summary>
        CoreImmutable,

        /// <summary>
        /// The referenced item is not present in the buffer.
        /// </summary>
        ItemNotInBuffer,

        /// <summary>
        /// The destination position is already occupied.
        /// </summary>
        DestinationNotEmpty,

        /// <summary>
        /// The move or remove origin holds nothing.
        /// </summary>
        OriginEmpty,

        /// <summary>
        /// A Structure does not fit the contiguous empty footprint required.
        /// </summary>
        InsufficientFootprint,

        /// <summary>
        /// The containment context of the edit is invalid.
        /// </summary>
        InvalidContainment,

        /// <summary>
        /// Placing a Structure inside another is not supported in this slice.
        /// </summary>
        NestingNotSupported,

        /// <summary>
        /// The two targets cannot be directly swapped.
        /// </summary>
        IncompatibleSwap,

        /// <summary>
        /// Removal is blocked because the buffer has no free capacity.
        /// </summary>
        BufferFull,

        /// <summary>
        /// The Overwrite target is an empty position.
        /// </summary>
        OverwriteTargetEmpty,

        /// <summary>
        /// The Overwrite target is not an eligible occupied player object.
        /// </summary>
        OverwriteTargetIneligible,

        /// <summary>
        /// The buffered item is not a tagged Overwrite Instruction.
        /// </summary>
        NotAnOverwriteInstruction,

        /// <summary>
        /// The buffered item is not a Directive.
        /// </summary>
        NotADirective
    }
}