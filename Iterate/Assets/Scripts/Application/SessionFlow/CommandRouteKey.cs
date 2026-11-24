using System;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// One row of the legality table: a command type in a stage.
    /// </summary>
    /// <param name="Command">The command type.</param>
    /// <param name="Stage">The stage it is legal in.</param>
    public readonly record struct CommandRouteKey(Type Command, SessionStage Stage);
}