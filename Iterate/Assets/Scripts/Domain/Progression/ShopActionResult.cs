namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of a free shop action — pinning or unpinning.
    /// </summary>
    /// <param name="Succeeded">Whether the action was applied.</param>
    /// <param name="Rejection">Why it was refused; None on success.</param>
    public sealed record ShopActionResult(bool Succeeded, ShopRejection Rejection);
}