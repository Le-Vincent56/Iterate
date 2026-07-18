namespace Iterate.Application.Logging
{
    /// <summary>
    /// The project's registered logging categories. Features add categories here as they first
    /// need them; ad-hoc inline categories are not used.
    /// </summary>
    public static class LogCategories
    {
        /// <summary>
        /// Application startup and composition-root activity.
        /// </summary>
        public static readonly LogCategory Boot = new("Boot");
    }
}