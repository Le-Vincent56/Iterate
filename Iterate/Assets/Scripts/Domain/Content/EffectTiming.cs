namespace Iterate.Domain.Content
{
    /// <summary>
    /// When an effect acts relative to the causing operation: a controlled causal band or a
    /// controlled named scheduling boundary. The name is a controlled string from
    /// <see cref="CatalogVocabulary"/>.
    /// </summary>
    /// <param name="Kind">Whether the name is a band or a named boundary.</param>
    /// <param name="Name">The controlled band or boundary token.</param>
    public sealed record EffectTiming(TimingKind Kind, string Name);
}