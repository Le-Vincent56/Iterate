namespace Iterate.Application.Content
{
    /// <summary>
    /// A pool a shop rerolls from while offering no Dependencies, recorded during the shops pass and
    /// checked against the pool's membership once every file has been read.
    /// </summary>
    /// <param name="File">The file that declared the binding.</param>
    /// <param name="JsonPath">The shop's JSON path.</param>
    /// <param name="PoolID">The bound pool's identity.</param>
    /// <param name="ShopID">The shop's identity, for the message.</param>
    public sealed record PendingDependencyFreePool(
        string File,
        string JsonPath,
        string PoolID,
        string ShopID
    );
}