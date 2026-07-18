namespace Iterate.Application.Logging
{
    /// <summary>
    /// Severity levels for the logging seam. Trace and Debug are compile-stripped from release
    /// builds; Off is a filter sentinel that silences a category entirely and is never callable.
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        Off = 5
    }
}