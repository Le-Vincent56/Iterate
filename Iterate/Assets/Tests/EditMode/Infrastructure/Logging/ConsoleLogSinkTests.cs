using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Iterate.Application.Logging;
using Iterate.Infrastructure.Logging;

namespace Iterate.Infrastructure.Logging.Tests
{
    /// <summary>
    /// Tests that <see cref="ConsoleLogSink"/> maps levels to the matching Unity console channel
    /// and formats category, fields, and exceptions into the emitted line.
    /// </summary>
    public sealed class ConsoleLogSinkTests
    {
        [Test]
        public void Write_Info_EmitsLogChannel()
        {
            ConsoleLogSink sink = new();
            LogEntry entry = CreateEntry(LogLevel.Info, "boot complete");
            LogAssert.Expect(LogType.Log, "[Boot] boot complete");

            sink.Write(in entry);
        }

        [Test]
        public void Write_Warning_EmitsWarningChannel()
        {
            ConsoleLogSink sink = new();
            LogEntry entry = CreateEntry(LogLevel.Warning, "odd state");
            LogAssert.Expect(LogType.Warning, "[Boot] odd state");

            sink.Write(in entry);
        }

        [Test]
        public void Write_Error_EmitsErrorChannel()
        {
            ConsoleLogSink sink = new();
            LogEntry entry = CreateEntry(LogLevel.Error, "failed");
            LogAssert.Expect(LogType.Error, "[Boot] failed");

            sink.Write(in entry);
        }

        [Test]
        public void Write_WithFields_AppendsKeyValuePairs()
        {
            ConsoleLogSink sink = new();
            LogCategory category = new("Boot");
            LogEntry entry = new(
                category,
                LogLevel.Info,
                "loaded",
                null,
                LogField.Of("count", 3),
                LogField.Of("name", "core"),
                default,
                2
            );
            LogAssert.Expect(LogType.Log, "[Boot] loaded | count=3 | name=core");

            sink.Write(in entry);
        }

        /// <summary>
        /// Creates a field-free Boot-category entry at the given level.
        /// </summary>
        /// <param name="level">The entry's severity.</param>
        /// <param name="message">The message.</param>
        /// <returns>The created entry.</returns>
        private static LogEntry CreateEntry(LogLevel level, string message)
        {
            LogCategory category = new("Boot");
            return new LogEntry(category, level, message, null, default, default, default, 0);
        }
    }
}
