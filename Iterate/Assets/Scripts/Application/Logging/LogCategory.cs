namespace Iterate.Application.Logging
{
    /// <summary>
    /// A named logging category a logger binds to; filtering resolves per category.
    /// </summary>
    /// <param name="Name">The category's stable display name.</param>
    public readonly record struct LogCategory(string Name);
}