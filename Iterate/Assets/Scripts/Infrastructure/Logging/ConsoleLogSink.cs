using System.Globalization;
using System.Text;
using UnityEngine;
using Iterate.Application.Logging;

namespace Iterate.Infrastructure.Logging
{
    /// <summary>
    /// The single console sink and the project's only <c>UnityEngine.Debug</c> call site.
    /// Formats an entry as "[Category] message | key=value | exception=..." and routes Error to
    /// the error channel, Warning to the warning channel, and everything else to the log channel.
    /// Safe to call from any thread: each write formats into its own local buffer, an allocation
    /// the no-hot-path-logging rule sanctions.
    /// </summary>
    public sealed class ConsoleLogSink : ILogSink
    {
        /// <summary>
        /// Formats and writes one entry to the Unity console.
        /// </summary>
        /// <param name="entry">The entry to write.</param>
        public void Write(in LogEntry entry)
        {
            StringBuilder builder = new(256);
            builder.Append('[').Append(entry.Category.Name).Append("] ").Append(entry.Message);

            for (int index = 0; index < entry.FieldCount; index++)
            {
                AppendField(builder, entry.FieldAt(index));
            }

            if (entry.Exception != null)
                builder.Append(" | exception=").Append(entry.Exception);

            string line = builder.ToString();

            switch (entry.Level)
            {
                case LogLevel.Error:
                    Debug.LogError(line);
                    break;
                
                case LogLevel.Warning:
                    Debug.LogWarning(line);
                    break;

                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Info:
                case LogLevel.Off:
                default:
                    Debug.Log(line);
                    break;
            }
        }

        /// <summary>
        /// Appends one structured field to the buffer as " | key=value", reading the slot the
        /// field's kind selects. Doubles format with the invariant culture so output is
        /// machine-independent.
        /// </summary>
        /// <param name="builder">The buffer being formatted into.</param>
        /// <param name="field">The field to append.</param>
        private static void AppendField(StringBuilder builder, in LogField field)
        {
            builder.Append(" | ").Append(field.Key).Append('=');

            switch (field.Kind)
            {
                case LogFieldKind.String:
                    builder.Append(field.StringValue);
                    break;
                
                case LogFieldKind.Int64:
                    builder.Append(field.Int64Value);
                    break;
                
                case LogFieldKind.Double:
                    builder.Append(field.DoubleValue.ToString(CultureInfo.InvariantCulture));
                    break;
                
                case LogFieldKind.Bool:
                    builder.Append(field.Int64Value != 0L);
                    break;
                
                case LogFieldKind.Boxed:
                    builder.Append(field.BoxedValue);
                    break;

                case LogFieldKind.None:
                default:
                    break;
            }
        }
    }
}