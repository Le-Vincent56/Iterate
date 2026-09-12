namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed reason a Process could not be created. Every one of these is a content or
    /// configuration fault the authored catalog should have prevented, surfaced as a value so the
    /// caller can name it rather than catching an exception.
    /// </summary>
    public enum ProcessCreationRejection
    {
        /// <summary>
        /// No rejection; the Process was created.
        /// </summary>
        None,

        /// <summary>
        /// The configuration names a Core the catalog does not define.
        /// </summary>
        CoreMissing,

        /// <summary>
        /// The Core carries a fixed Structure line, which this slice cannot materialise. Lifted by the
        /// Core source objects child.
        /// </summary>
        CoreStructureUnsupported,

        /// <summary>
        /// A pre-installed source entry could not be installed at its authored position.
        /// </summary>
        InitialSourceIllegal,

        /// <summary>
        /// Scripted content named by the configuration is not held in the Repository.
        /// </summary>
        ScriptedContentMissing,

        /// <summary>
        /// The configuration declares a drawn Buffer load but no confirmed Active Branch was supplied.
        /// </summary>
        BranchMissing,

        /// <summary>
        /// The confirmed Active Branch holds no unexposed entry of a guaranteed definition. Guaranteed
        /// content is never drawn around (CAB-EVT-865).
        /// </summary>
        GuaranteedContentMissing
    }
}