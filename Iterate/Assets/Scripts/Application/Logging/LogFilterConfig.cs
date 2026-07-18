using System;
using System.Collections.Generic;

namespace Iterate.Application.Logging
{
    /// <summary>
    /// Immutable filtering configuration: a global default minimum level plus per-category
    /// overrides. A logger resolves its minimum once at creation. An Off override silences a
    /// category entirely, including its Error entries.
    /// </summary>
    public sealed class LogFilterConfig
    {
        private static readonly Dictionary<string, LogLevel> _emptyOverrides = new();

        private readonly LogLevel _defaultMinimumLevel;

        private readonly IReadOnlyDictionary<string, LogLevel> _categoryOverrides;

        public LogFilterConfig(LogLevel defaultMinimumLevel) : this(defaultMinimumLevel, _emptyOverrides) { }

        public LogFilterConfig(LogLevel defaultMinimumLevel, IReadOnlyDictionary<string, LogLevel> categoryOverrides)
        {
            _defaultMinimumLevel = defaultMinimumLevel;
            _categoryOverrides = categoryOverrides ?? throw new ArgumentNullException(nameof(categoryOverrides));
        }

        /// <summary>
        /// Resolves the minimum level for a category: its override when one exists, the global
        /// default otherwise.
        /// </summary>
        /// <param name="category">The category to resolve.</param>
        /// <returns>The minimum level entries in that category must meet.</returns>
        public LogLevel MinimumLevelFor(in LogCategory category)
        {
            return _categoryOverrides.GetValueOrDefault(category.Name, _defaultMinimumLevel);
        }
    }
}