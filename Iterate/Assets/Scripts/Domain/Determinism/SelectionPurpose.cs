using Iterate.Domain.Content;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The registry of recognized selection purposes, seeded verbatim with the nine purposes
    /// (including the two "— non-gameplay" suffixes). The purpose vocabulary is extensible content —
    /// adding a purpose is a reviewable edit here — unlike the closed <see cref="SelectionMethod"/> set.
    /// Membership is ordinal and case-sensitive.
    /// </summary>
    public static class SelectionPurposes
    {
        /// <summary>
        /// The controlled set of recognized selection purposes.
        /// </summary>
        public static ControlledVocabulary All { get; } = new ControlledVocabulary(
            "Active Branch exposure",
            "Basic Indexing",
            "Shop offer replacement",
            "Reward generation",
            "Random target selection",
            "Random ordering",
            "Future runtime effect",
            "Simulation policy sampling — non-gameplay",
            "Presentation variation — non-gameplay"
        );
    }
}