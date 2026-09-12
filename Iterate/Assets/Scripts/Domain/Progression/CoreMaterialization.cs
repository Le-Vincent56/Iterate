using Iterate.Domain.Compilation;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of materialising a Core into a source arrangement: the arrangement and its
    /// designated final output, or the typed reason the Core cannot be materialised.
    /// </summary>
    /// <param name="Succeeded">Whether the Core was materialised.</param>
    /// <param name="Rejection">The rejection reason; None on success.</param>
    /// <param name="Arrangement">The arrangement of Core and empty slots; null on rejection.</param>
    /// <param name="FinalOutput">The designated final Core output position.</param>
    public readonly record struct CoreMaterialization(
        bool Succeeded,
        ProcessCreationRejection Rejection,
        SourceArrangement Arrangement,
        SourcePosition FinalOutput
    )
    {
        /// <summary>
        /// Creates a successful materialisation.
        /// </summary>
        /// <param name="arrangement">The produced arrangement.</param>
        /// <param name="finalOutput">The designated final output position.</param>
        /// <returns>The materialisation.</returns>
        public static CoreMaterialization Success(SourceArrangement arrangement, SourcePosition finalOutput)
        {
            return new CoreMaterialization(true, ProcessCreationRejection.None, arrangement, finalOutput);
        }

        /// <summary>
        /// Creates a rejected materialisation.
        /// </summary>
        /// <param name="rejection">The rejection reason.</param>
        /// <returns>The materialisation.</returns>
        public static CoreMaterialization Rejected(ProcessCreationRejection rejection)
        {
            return new CoreMaterialization(false, rejection, null, default);
        }
    }
}