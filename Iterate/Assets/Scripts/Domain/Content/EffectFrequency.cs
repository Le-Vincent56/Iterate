namespace Iterate.Domain.Content
{
    /// <summary>
    /// How often an effect may commit and the reset scope of its allowance. Allowance is the
    /// frequency form and scope is the declared reset scope, both controlled strings from
    /// <see cref="CatalogVocabulary"/>.
    /// </summary>
    /// <param name="Allowance">The frequency-form token.</param>
    /// <param name="Scope">The reset-scope token.</param>
    public sealed record EffectFrequency(string Allowance, string Scope);
}