using System;
using System.Diagnostics;

namespace Iterate.Application.Logging
{
    /// <summary>
    /// The ergonomic level-named facade over <see cref="IGameLogger"/>, fixed arity 0..3
    /// structured fields. Trace and Debug are compile-stripped outside the Editor and
    /// development builds, which also strips evaluation of their arguments — verbose-log
    /// arguments must therefore be side-effect-free.
    /// </summary>
    public static class GameLog
    {
        /// <summary>
        /// Logs a Trace entry. Stripped from release builds.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Trace(this IGameLogger logger, string message)
        {
            Write(logger, LogLevel.Trace, message, null, default, default, default, 0);
        }

        /// <summary>
        /// Logs a Trace entry with one field. Stripped from release builds.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="field0">The structured field.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Trace(this IGameLogger logger, string message, in LogField field0)
        {
            Write(logger, LogLevel.Trace, message, null, field0, default, default, 1);
        }

        /// <summary>
        /// Logs a Trace entry with two fields. Stripped from release builds.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="field0">The first structured field.</param>
        /// <param name="field1">The second structured field.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Trace(
            this IGameLogger logger,
            string message,
            in LogField field0,
            in LogField field1
        )
        {
            Write(logger, LogLevel.Trace, message, null, field0, field1, default, 2);
        }

        /// <summary>
        /// Logs a Trace entry with three fields. Stripped from release builds.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="field0">The first structured field.</param>
        /// <param name="field1">The second structured field.</param>
        /// <param name="field2">The third structured field.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Trace(
            this IGameLogger logger,
            string message,
            in LogField field0,
            in LogField field1,
            in LogField field2
        )
        {
            Write(logger, LogLevel.Trace, message, null, field0, field1, field2, 3);
        }

        /// <summary>
        /// Logs a Debug entry. Stripped from release builds.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Debug(this IGameLogger logger, string message)
        {
            Write(logger, LogLevel.Debug, message, null, default, default, default, 0);
        }

        /// <summary>
        /// Logs a Debug entry with one field. Stripped from release builds.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="field0">The structured field.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Debug(this IGameLogger logger, string message, in LogField field0)
        {
            Write(logger, LogLevel.Debug, message, null, field0, default, default, 1);
        }

        /// <summary>
        /// Logs a Debug entry with two fields. Stripped from release builds.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="field0">The first structured field.</param>
        /// <param name="field1">The second structured field.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Debug(
            this IGameLogger logger,
            string message,
            in LogField field0,
            in LogField field1
        )
        {
            Write(logger, LogLevel.Debug, message, null, field0, field1, default, 2);
        }

        /// <summary>
        /// Logs a Debug entry with three fields. Stripped from release builds.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="field0">The first structured field.</param>
        /// <param name="field1">The second structured field.</param>
        /// <param name="field2">The third structured field.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Debug(
            this IGameLogger logger,
            string message,
            in LogField field0,
            in LogField field1,
            in LogField field2
        )
        {
            Write(logger, LogLevel.Debug, message, null, field0, field1, field2, 3);
        }

        /// <summary>
        /// Logs an Info entry.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        public static void Info(this IGameLogger logger, string message)
        {
            Write(logger, LogLevel.Info, message, null, default, default, default, 0);
        }

        /// <summary>
        /// Logs an Info entry with one field.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="field0">The structured field.</param>
        public static void Info(this IGameLogger logger, string message, in LogField field0)
        {
            Write(logger, LogLevel.Info, message, null, field0, default, default, 1);
        }

        /// <summary>
        /// Logs an Info entry with two fields.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="field0">The first structured field.</param>
        /// <param name="field1">The second structured field.</param>
        public static void Info(
            this IGameLogger logger,
            string message,
            in LogField field0,
            in LogField field1
        )
        {
            Write(logger, LogLevel.Info, message, null, field0, field1, default, 2);
        }

        /// <summary>
        /// Logs an Info entry with three fields.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="field0">The first structured field.</param>
        /// <param name="field1">The second structured field.</param>
        /// <param name="field2">The third structured field.</param>
        public static void Info(
            this IGameLogger logger,
            string message,
            in LogField field0,
            in LogField field1,
            in LogField field2
        )
        {
            Write(logger, LogLevel.Info, message, null, field0, field1, field2, 3);
        }

        /// <summary>
        /// Logs a Warning entry.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        public static void Warning(this IGameLogger logger, string message)
        {
            Write(logger, LogLevel.Warning, message, null, default, default, default, 0);
        }

        /// <summary>
        /// Logs a Warning entry with one field.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="field0">The structured field.</param>
        public static void Warning(this IGameLogger logger, string message, in LogField field0)
        {
            Write(logger, LogLevel.Warning, message, null, field0, default, default, 1);
        }

        /// <summary>
        /// Logs a Warning entry with two fields.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="field0">The first structured field.</param>
        /// <param name="field1">The second structured field.</param>
        public static void Warning(
            this IGameLogger logger,
            string message,
            in LogField field0,
            in LogField field1
        )
        {
            Write(logger, LogLevel.Warning, message, null, field0, field1, default, 2);
        }

        /// <summary>
        /// Logs a Warning entry with three fields.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="field0">The first structured field.</param>
        /// <param name="field1">The second structured field.</param>
        /// <param name="field2">The third structured field.</param>
        public static void Warning(
            this IGameLogger logger,
            string message,
            in LogField field0,
            in LogField field1,
            in LogField field2
        )
        {
            Write(logger, LogLevel.Warning, message, null, field0, field1, field2, 3);
        }

        /// <summary>
        /// Logs an Error entry carrying an exception.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="exception">The failure being reported.</param>
        public static void Error(this IGameLogger logger, string message, Exception exception)
        {
            Write(logger, LogLevel.Error, message, exception, default, default, default, 0);
        }

        /// <summary>
        /// Logs an Error entry carrying an exception and one field.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="exception">The failure being reported.</param>
        /// <param name="field0">The structured field.</param>
        public static void Error(
            this IGameLogger logger,
            string message,
            Exception exception,
            in LogField field0
        )
        {
            Write(logger, LogLevel.Error, message, exception, field0, default, default, 1);
        }

        /// <summary>
        /// Logs an Error entry carrying an exception and two fields.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="exception">The failure being reported.</param>
        /// <param name="field0">The first structured field.</param>
        /// <param name="field1">The second structured field.</param>
        public static void Error(
            this IGameLogger logger,
            string message,
            Exception exception,
            in LogField field0,
            in LogField field1
        )
        {
            Write(logger, LogLevel.Error, message, exception, field0, field1, default, 2);
        }

        /// <summary>
        /// Logs an Error entry carrying an exception and three fields.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="exception">The failure being reported.</param>
        /// <param name="field0">The first structured field.</param>
        /// <param name="field1">The second structured field.</param>
        /// <param name="field2">The third structured field.</param>
        public static void Error(
            this IGameLogger logger,
            string message,
            Exception exception,
            in LogField field0,
            in LogField field1,
            in LogField field2
        )
        {
            Write(logger, LogLevel.Error, message, exception, field0, field1, field2, 3);
        }

        /// <summary>
        /// Builds the entry and hands it to the logger, whose level gate decides whether it
        /// reaches the sink.
        /// </summary>
        /// <param name="logger">The target logger.</param>
        /// <param name="level">The entry's severity.</param>
        /// <param name="message">The message.</param>
        /// <param name="exception">The carried exception, or null.</param>
        /// <param name="field0">The first field slot.</param>
        /// <param name="field1">The second field slot.</param>
        /// <param name="field2">The third field slot.</param>
        /// <param name="fieldCount">How many field slots are populated.</param>
        private static void Write(
            IGameLogger logger,
            LogLevel level,
            string message,
            Exception exception,
            in LogField field0,
            in LogField field1,
            in LogField field2,
            int fieldCount
        )
        {
            logger.Log(new LogEntry(logger.Category, level, message, exception, field0, field1, field2, fieldCount));
        }
    }
}