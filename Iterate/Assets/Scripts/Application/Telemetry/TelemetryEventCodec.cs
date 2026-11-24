using System;
using System.Globalization;
using Iterate.Application.Content.Json;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Encodes a telemetry event as one compact JSON line, with its members in UX §28's fixed order
    /// and every absent member written as null rather than omitted.
    /// </summary>
    public static class TelemetryEventCodec
    {
        private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        private static readonly JsonObject _emptyData = new JsonObjectBuilder().Build();

        /// <summary>
        /// Encodes one event.
        /// </summary>
        /// <param name="value">The event to encode.</param>
        /// <returns>One JSON line, ending in a single newline.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the event is null.</exception>
        public static string Encode(TelemetryEvent value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            JsonObjectBuilder builder = new();
            builder.Add("run", value.Run);
            builder.Add("seq", value.Sequence);
            builder.Add("type", value.Type);
            builder.Add("at", value.At.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture));
            builder.Add("ms", value.Milliseconds);
            builder.Add("build", value.Build);
            builder.Add("ux", value.UX);
            builder.Add("content", value.Content);
            builder.Add("mode", value.Mode);
            builder.Add("stage", value.Stage);
            builder.Add("process", value.Process);
            builder.Add("execution", value.Execution.HasValue ? JsonValues.Integer(value.Execution.Value) : JsonValues.Null());
            builder.AddNull("input");
            builder.Add("subject", value.Subject);
            builder.Add("target", value.Target);
            builder.Add("result", value.Result);
            builder.Add("reason", value.Reason);
            builder.Add("resources", ResourcesOf(value.Resources));
            builder.Add("configuration", value.Configuration);
            builder.Add("data", value.Data ?? _emptyData);
            return JsonWriter.WriteLine(builder.Build());
        }

        /// <summary>
        /// Encodes the resources member.
        /// </summary>
        /// <param name="resources">The resources, or null.</param>
        /// <returns>The resources object, or JSON null.</returns>
        private static JsonValue ResourcesOf(TelemetryResources resources)
        {
            if (resources == null)
                return JsonValues.Null();

            return new JsonObjectBuilder()
                .Add("bytes", resources.Bytes)
                .Add("tokens", resources.Tokens)
                .Add("ramUsed", resources.RAMUsed)
                .Add("ramCapacity", resources.RAMCapacity)
                .Build();
        }
    }
}