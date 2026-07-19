namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// The base of the parsed JSON value tree. Every node records the 1-indexed line and column of
    /// its first character in the source document.
    /// </summary>
    /// <param name="Line">The 1-indexed source line of the node's first character.</param>
    /// <param name="Column">The 1-indexed source column of the node's first character.</param>
    public abstract record JsonValue(int Line, int Column);
}