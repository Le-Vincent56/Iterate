namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// A JSON boolean.
    /// </summary>
    /// <param name="Line">The 1-indexed source line of the literal.</param>
    /// <param name="Column">The 1-indexed source column of the literal.</param>
    /// <param name="Value">The boolean value.</param>
    public sealed record JsonBool(int Line, int Column, bool Value) : JsonValue(Line, Column);
}