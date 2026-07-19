namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// A JSON null literal.
    /// </summary>
    /// <param name="Line">The 1-indexed source line of the literal.</param>
    /// <param name="Column">The 1-indexed source column of the literal.</param>
    public sealed record JsonNull(int Line, int Column) : JsonValue(Line, Column);
}