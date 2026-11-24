using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Pays for the shop's duplicate Service to add a copy of an owned item to the Repository.
    /// </summary>
    /// <param name="Item">The item to duplicate.</param>
    public sealed record DuplicateItem(InstanceID Item) : ISessionCommand;
}