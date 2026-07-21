namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The controlled set of quantity operation types a change may declare. The operation is recorded
    /// distinctly from the resulting delta, so a resolved no-op is still classified by intent.
    /// </summary>
    public enum QuantityOperationType
    {
        /// <summary>
        /// Increase by a relative amount.
        /// </summary>
        Increase,

        /// <summary>
        /// Decrease by a relative amount.
        /// </summary>
        Decrease,

        /// <summary>
        /// Gain a resource.
        /// </summary>
        Gain,

        /// <summary>
        /// Spend a resource.
        /// </summary>
        Spend,

        /// <summary>
        /// Refund a previously spent resource.
        /// </summary>
        Refund,

        /// <summary>
        /// Set to an absolute value.
        /// </summary>
        Set,

        /// <summary>
        /// Assign an absolute value to a variable.
        /// </summary>
        Assign,

        /// <summary>
        /// Transfer between two quantities.
        /// </summary>
        Transfer,

        /// <summary>
        /// Reserve capacity.
        /// </summary>
        Reserve,

        /// <summary>
        /// Release reserved capacity.
        /// </summary>
        Release,

        /// <summary>
        /// Increment by one.
        /// </summary>
        Increment,

        /// <summary>
        /// Decrement by one.
        /// </summary>
        Decrement
    }
}