using System.Collections.Generic;

namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// A JSON array: its items in document order.
    /// </summary>
    /// <param name="Line">The 1-indexed source line of the opening bracket.</param>
    /// <param name="Column">The 1-indexed source column of the opening bracket.</param>
    /// <param name="Items">The array items in document order.</param>
    public sealed record JsonArray(int Line, int Column, IReadOnlyList<JsonValue> Items) : JsonValue(Line, Column);
}