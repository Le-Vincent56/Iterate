namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// A JSON string with its escapes decoded.
    /// </summary>
    /// <param name="Line">The 1-indexed source line of the opening quote.</param>
    /// <param name="Column">The 1-indexed source column of the opening quote.</param>
    /// <param name="Value">The decoded string value.</param>
    public sealed record JsonString(int Line, int Column, string Value) : JsonValue(Line, Column);
}