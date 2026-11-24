namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Submits the reviewed result as the Process's final result, ending the Process.
    /// </summary>
    public sealed record Submit : ISessionCommand;
}