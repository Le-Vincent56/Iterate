namespace Iterate.Domain.Content
{
    /// <summary>
    /// One pre-installed source entry: the Core position the content occupies when the Process
    /// opens. Installed through the ordinary Compilation install edit, so a starting arrangement is
    /// never built by a second path.
    /// </summary>
    /// <param name="Position">The one-based Core position the content occupies.</param>
    /// <param name="Content">The content ID installed at that position.</param>
    public sealed record InitialSourceSpec(int Position, string Content);
}