namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Compiles the current source, pays its cost in Bytes, runs one execution and applies its
    /// outcome to the Process.
    /// </summary>
    public sealed record CompileAndExecute : ISessionCommand;
}