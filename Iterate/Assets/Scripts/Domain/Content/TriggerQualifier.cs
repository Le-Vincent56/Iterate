namespace Iterate.Domain.Content
{
    /// <summary>
    /// A controlled qualifier narrowing an effect's trigger match. Kind and value are controlled
    /// strings validated against <see cref="CatalogVocabulary"/>.
    /// </summary>
    /// <param name="Kind">The qualifier kind token.</param>
    /// <param name="Value">The qualifier value token.</param>
    public sealed record TriggerQualifier(string Kind, string Value);
}