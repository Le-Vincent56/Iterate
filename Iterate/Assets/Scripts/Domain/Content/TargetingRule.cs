namespace Iterate.Domain.Content
{
    /// <summary>
    /// The rule by which an effect selects its target. Kind is a controlled string from
    /// <see cref="CatalogVocabulary"/>; argument carries any parameter the kind requires (empty when
    /// none).
    /// </summary>
    /// <param name="Kind">The targeting kind token.</param>
    /// <param name="Argument">The kind's parameter, or the empty string when none applies.</param>
    public sealed record TargetingRule(string Kind, string Argument);
}