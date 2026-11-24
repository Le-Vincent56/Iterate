using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Pays for the shop's delete Service to remove an owned item from the Repository.
    /// </summary>
    /// <param name="Item">The item to delete.</param>
    public sealed record DeleteItem(InstanceID Item) : ISessionCommand;
}