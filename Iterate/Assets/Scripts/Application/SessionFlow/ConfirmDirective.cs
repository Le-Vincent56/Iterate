using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Activates a Directive held in the Instruction Buffer.
    /// </summary>
    /// <param name="Directive">The Directive to activate.</param>
    public sealed record ConfirmDirective(InstanceID Directive) : ISessionCommand;
}