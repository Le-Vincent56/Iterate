namespace Iterate.Domain.Content
{
    /// <summary>
    /// The controlled rule naming which host classes a Patch may socket to.
    /// </summary>
    /// <param name="Rule">The host-eligibility rule token.</param>
    public sealed record PatchHostEligibility(string Rule);
}