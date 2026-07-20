namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The closed set of selection methods. A seventh method is a canon
    /// revision, not a new enum value added at implementation convenience.
    /// </summary>
    public enum SelectionMethod
    {
        /// <summary>
        /// Equal probability over the captured set; one candidate is selected.
        /// </summary>
        UniformSingleSelection,

        /// <summary>
        /// Per-draw equal probability over the remaining candidates; each selected candidate is removed.
        /// </summary>
        UniformSelectionWithoutReplacement,

        /// <summary>
        /// Per-draw equal probability; the selected candidate remains eligible and may repeat.
        /// </summary>
        UniformSelectionWithReplacement,

        /// <summary>
        /// Selection proportional to the exact captured final weights.
        /// </summary>
        WeightedSelection,

        /// <summary>
        /// One complete reproducible permutation of the captured set.
        /// </summary>
        DeterministicShuffle,

        /// <summary>
        /// A reproducible ordering of the captured finite set with a declared prefix consumed.
        /// </summary>
        RandomOrderingOfCapturedFiniteSet
    }
}