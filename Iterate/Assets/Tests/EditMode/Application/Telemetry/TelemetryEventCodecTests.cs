using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.Telemetry;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// Tests <see cref="TelemetryEventCodec"/>: one compact line per event, the members in UX §28's
    /// order, every absent member written as null, <c>input</c> always null, and <c>at</c> in UTC
    /// with milliseconds whatever the current culture.
    /// </summary>
    public sealed class TelemetryEventCodecTests
    {
        private static readonly DateTimeOffset _at = new DateTimeOffset(new DateTime(2026, 11, 24), TimeSpan.FromHours(2)).Add(new TimeSpan(0, 15, 5, 9, 7));

        [Test]
        public void Encode_FullEvent_WritesTheGoldenLine()
        {
            TelemetryEvent value = new(
                "run-1",
                3,
                TelemetryEventTypes.Command,
                _at,
                1234L,
                "0.1.0",
                "0.1",
                "fixture-0.0.1",
                "onboarding",
                "Build",
                "WB-PROC-001",
                2,
                "InstallLine",
                "#7",
                "refused",
                "WrongStage",
                new TelemetryResources(5, 3, 1, 2),
                "cfg-2",
                new JsonObjectBuilder().Add("line", 2L).Build()
            );

            string line = TelemetryEventCodec.Encode(value);

            Assert.AreEqual(
                "{\"run\":\"run-1\",\"seq\":3,\"type\":\"command\",\"at\":\"2026-11-24T13:05:09.007Z\",\"ms\":1234,"
                + "\"build\":\"0.1.0\",\"ux\":\"0.1\",\"content\":\"fixture-0.0.1\",\"mode\":\"onboarding\",\"stage\":\"Build\","
                + "\"process\":\"WB-PROC-001\",\"execution\":2,\"input\":null,\"subject\":\"InstallLine\",\"target\":\"#7\","
                + "\"result\":\"refused\",\"reason\":\"WrongStage\",\"resources\":{\"bytes\":5,\"tokens\":3,\"ramUsed\":1,\"ramCapacity\":2},"
                + "\"configuration\":\"cfg-2\",\"data\":{\"line\":2}}\n",
                line
            );
        }

        [Test]
        public void Encode_EventWithNothingOptional_WritesEveryMemberAsNull()
        {
            string line = TelemetryEventCodec.Encode(Minimal());

            Assert.AreEqual(
                "{\"run\":\"run-1\",\"seq\":0,\"type\":\"run_started\",\"at\":\"2026-11-24T13:05:09.007Z\",\"ms\":0,"
                + "\"build\":null,\"ux\":null,\"content\":null,\"mode\":null,\"stage\":null,\"process\":null,\"execution\":null,"
                + "\"input\":null,\"subject\":null,\"target\":null,\"result\":null,\"reason\":null,\"resources\":null,"
                + "\"configuration\":null,\"data\":{}}\n",
                line
            );
        }

        [Test]
        public void Encode_AnyEvent_HoldsNoNewlineButTheLast()
        {
            TelemetryEvent value = Minimal() with { Subject = "line\nbreak", Data = new JsonObjectBuilder().Add("note", "a\nb").Build() };

            string line = TelemetryEventCodec.Encode(value);

            Assert.AreEqual(line.Length - 1, line.IndexOf('\n'));
            StringAssert.Contains("\"input\":null", line);
        }

        [Test]
        public void Encode_UnderAnotherCulture_WritesTheInvariantTime()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("fi-FI");
            try
            {
                StringAssert.Contains("\"at\":\"2026-11-24T13:05:09.007Z\"", TelemetryEventCodec.Encode(Minimal()));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void Encode_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => TelemetryEventCodec.Encode(null));
        }

        /// <summary>
        /// An event with every optional member absent.
        /// </summary>
        /// <returns>The event.</returns>
        private static TelemetryEvent Minimal()
        {
            return new TelemetryEvent(
                "run-1",
                0,
                TelemetryEventTypes.RunStarted,
                _at,
                0L,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );
        }
    }
}
