namespace Iterate.Domain.Content
{
    /// <summary>
    /// A gain of a named resource. The minimal primitive a build-interaction effect needs: it names
    /// what is gained and how much, and nothing about when — the effect's trigger and frequency say
    /// that. Only BYTES is a legal resource at this slice; the validator is what holds that line.
    /// </summary>
    /// <param name="Resource">The resource token gained.</param>
    /// <param name="Amount">How much is gained; at least one.</param>
    public sealed record ResourceGainOperation(string Resource, int Amount) : EffectOperation
    {
        /// <summary>
        /// The operation primitive this record represents.
        /// </summary>
        public override OperationKind Kind => OperationKind.ResourceGain;
    }
}