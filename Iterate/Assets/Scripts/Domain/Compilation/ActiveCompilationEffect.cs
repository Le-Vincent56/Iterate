using System.Globalization;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// An active COMPILATION-domain effect paired with a per-source-instance ledger key. The private
    /// constructor and single factory force the instance component into the key, so two installed copies of
    /// one definition ledger independently and a key without an instance component cannot be constructed.
    /// </summary>
    public sealed record ActiveCompilationEffect
    {
        /// <summary>
        /// The per-source-instance ledger key: definition identity, effect index, and instance ordinal.
        /// </summary>
        public string SourceKey { get; }

        /// <summary>
        /// The source's display name, carried into the breakdown for disclosure.
        /// </summary>
        public string SourceDisplayName { get; }

        /// <summary>
        /// The effect definition this active effect resolves.
        /// </summary>
        public EffectDefinition Effect { get; }

        private ActiveCompilationEffect(string sourceKey, string sourceDisplayName, EffectDefinition effect)
        {
            SourceKey = sourceKey;
            SourceDisplayName = sourceDisplayName;
            Effect = effect;
        }

        /// <summary>
        /// Creates an active effect, forcing a per-source-instance ledger key.
        /// </summary>
        /// <param name="definitionID">The source definition's surrogate identity.</param>
        /// <param name="effectIndex">The effect's index within the definition.</param>
        /// <param name="sourceInstance">The source instance identity.</param>
        /// <param name="sourceDisplayName">The source's display name.</param>
        /// <param name="effect">The effect definition.</param>
        /// <returns>An active effect whose key carries the instance component.</returns>
        public static ActiveCompilationEffect For(
            string definitionID,
            int effectIndex,
            InstanceID sourceInstance,
            string sourceDisplayName,
            EffectDefinition effect)
        {
            string sourceKey = definitionID
                + ":" + effectIndex.ToString(CultureInfo.InvariantCulture)
                + "#" + sourceInstance.Value.ToString(CultureInfo.InvariantCulture);
            return new ActiveCompilationEffect(sourceKey, sourceDisplayName, effect);
        }
    }
}