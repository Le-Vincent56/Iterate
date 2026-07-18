using System;
using NUnit.Framework;
using Iterate.Application.Logging;

namespace Iterate.Application.Logging.Tests
{
    /// <summary>
    /// Tests that the <see cref="GameLog"/> facade routes level, message, exception, and fields
    /// into the entries the sink receives.
    /// </summary>
    public sealed class GameLogTests
    {
        [Test]
        public void Info_NoFields_WritesEntry()
        {
            FakeLogSink sink = new();
            IGameLogger logger = CreateLogger(sink, LogLevel.Trace);

            logger.Info("boot complete");

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreEqual(LogLevel.Info, sink.Entries[0].Level);
            Assert.AreEqual("boot complete", sink.Entries[0].Message);
            Assert.AreEqual(0, sink.Entries[0].FieldCount);
        }

        [Test]
        public void Warning_ThreeFields_CapturesAllInOrder()
        {
            FakeLogSink sink = new();
            IGameLogger logger = CreateLogger(sink, LogLevel.Trace);

            logger.Warning("odd state", LogField.Of("a", 1), LogField.Of("b", 2), LogField.Of("c", 3));

            Assert.AreEqual(3, sink.Entries[0].FieldCount);
            Assert.AreEqual("a", sink.Entries[0].FieldAt(0).Key);
            Assert.AreEqual("b", sink.Entries[0].FieldAt(1).Key);
            Assert.AreEqual("c", sink.Entries[0].FieldAt(2).Key);
        }

        [Test]
        public void Error_CarriesException()
        {
            FakeLogSink sink = new();
            IGameLogger logger = CreateLogger(sink, LogLevel.Trace);
            InvalidOperationException failure = new("boom");

            logger.Error("save failed", failure, LogField.Of("slot", 2));

            Assert.AreEqual(LogLevel.Error, sink.Entries[0].Level);
            Assert.AreSame(failure, sink.Entries[0].Exception);
            Assert.AreEqual(1, sink.Entries[0].FieldCount);
        }

        [Test]
        public void TraceAndDebug_CompileAndRouteInEditor()
        {
            FakeLogSink sink = new();
            IGameLogger logger = CreateLogger(sink, LogLevel.Trace);

            logger.Trace("trace line");
            logger.Debug("debug line", LogField.Of("n", 1));

            Assert.AreEqual(2, sink.Entries.Count);
        }

        /// <summary>
        /// Creates a logger bound to a test category over the given sink and minimum level.
        /// </summary>
        /// <param name="sink">The recording sink.</param>
        /// <param name="minimumLevel">The global minimum level for the logger.</param>
        /// <returns>The created logger.</returns>
        private static IGameLogger CreateLogger(FakeLogSink sink, LogLevel minimumLevel)
        {
            GameLoggerFactory factory = new(sink, new LogFilterConfig(minimumLevel));
            LogCategory category = new("Test");
            return factory.Create(in category);
        }
    }
}
