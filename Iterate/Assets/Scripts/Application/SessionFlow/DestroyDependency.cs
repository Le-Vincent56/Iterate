using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Destroys an installed Dependency, releasing its RAM and refunding Tokens where the rules allow.
    /// </summary>
    /// <param name="Dependency">The Dependency to destroy.</param>
    public sealed record DestroyDependency(InstanceID Dependency) : ISessionCommand;
}