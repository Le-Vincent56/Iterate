namespace Iterate.Application.Content
{
    /// <summary>
    /// One catalog validation failure: the file it occurred in, the JSON path to the offending node,
    /// the stable rule name, and a human-readable message. The validator returns the complete list of
    /// these, never first-failure-only.
    /// </summary>
    /// <param name="File">The catalog file the failure occurred in.</param>
    /// <param name="JsonPath">The JSON path to the offending node.</param>
    /// <param name="RuleName">The stable, code-internal rule identifier.</param>
    /// <param name="Message">The human-readable description of the failure.</param>
    public readonly record struct CatalogError(
        string File,
        string JsonPath,
        string RuleName,
        string Message
    );
}