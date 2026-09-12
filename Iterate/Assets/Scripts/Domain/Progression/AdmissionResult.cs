namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of offering one item to the Buffer. An admission is never rejected: a full Buffer
    /// holds the item rather than refusing it, which is what makes overflow a state to resolve rather
    /// than an error to handle.
    /// </summary>
    /// <param name="Outcome">Whether the item took a slot or is being held.</param>
    /// <param name="SlotNumber">The slot taken; zero when the item is held.</param>
    /// <param name="Item">The offered item.</param>
    public readonly record struct AdmissionResult(AdmissionOutcome Outcome, int SlotNumber, RepositoryItem Item)
    {
        /// <summary>
        /// Creates a result for an item that took a slot.
        /// </summary>
        /// <param name="slotNumber">The slot taken.</param>
        /// <param name="item">The admitted item.</param>
        /// <returns>The admission result.</returns>
        public static AdmissionResult Admitted(int slotNumber, RepositoryItem item)
        {
            return new AdmissionResult(AdmissionOutcome.Admitted, slotNumber, item);
        }

        /// <summary>
        /// Creates a result for an item held outside a full Buffer.
        /// </summary>
        /// <param name="item">The held item.</param>
        /// <returns>The admission result.</returns>
        public static AdmissionResult Held(RepositoryItem item)
        {
            return new AdmissionResult(AdmissionOutcome.Held, 0, item);
        }
    }
}