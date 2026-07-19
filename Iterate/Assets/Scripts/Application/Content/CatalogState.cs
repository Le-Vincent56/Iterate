namespace Iterate.Application.Content
{
    /// <summary>
    /// The lifecycle state of the catalog holder: not yet loaded, successfully loaded, or terminally
    /// failed.
    /// </summary>
    public enum CatalogState
    {
        Unloaded,
        Loaded,
        Failed
    }
}