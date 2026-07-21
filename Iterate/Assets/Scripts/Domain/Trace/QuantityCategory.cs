namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The controlled classification of the quantity a change acts on. Grounds the counter-history
    /// derivation in a fixed vocabulary rather than an inferred naming convention.
    /// </summary>
    public enum QuantityCategory
    {
        /// <summary>
        /// A runtime variable local to an execution.
        /// </summary>
        RuntimeVariable,

        /// <summary>
        /// A spendable resource such as Bytes.
        /// </summary>
        SpendableResource,

        /// <summary>
        /// A capacity limit such as RAM.
        /// </summary>
        Capacity,

        /// <summary>
        /// A Process-level counter.
        /// </summary>
        ProcessCounter,

        /// <summary>
        /// A structural capacity such as an Instruction Buffer size.
        /// </summary>
        StructuralCapacity,

        /// <summary>
        /// An output result register such as Value, Signal, or Score.
        /// </summary>
        OutputResult
    }
}