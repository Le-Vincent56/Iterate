using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Logging;

namespace Iterate.Application.Logging.Tests
{
    /// <summary>
    /// Tests category binding, level gating, Off silencing, and argument validation of
    /// <see cref="GameLoggerFactory"/> and the loggers it creates.
    /// </summary>
    public sealed class GameLoggerFactoryTests
    {
        [Test]
        public void Create_BindsCategory()
        {
            GameLoggerFactory factory = new(new FakeLogSink(), new LogFilterConfig(LogLevel.Info));
            LogCategory category = new("Boot");

            IGameLogger logger = factory.Create(in category);

            Assert.AreEqual(category, logger.Category);
        }

        [Test]
        public void Create_DefaultCategory_Throws()
        {
            GameLoggerFactory factory = new(new FakeLogSink(), new LogFilterConfig(LogLevel.Info));
            LogCategory category = default;

            Assert.Throws<ArgumentException>(() => factory.Create(in category));
        }

        [Test]
        public void Constructor_NullSink_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new GameLoggerFactory(null, new LogFilterConfig(LogLevel.Info)));
        }

        [Test]
        public void Constructor_NullFilterConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new GameLoggerFactory(new FakeLogSink(), null));
        }

        [Test]
        public void IsEnabled_BelowMinimum_IsFalse()
        {
            GameLoggerFactory factory = new(new FakeLogSink(), new LogFilterConfig(LogLevel.Info));
            LogCategory category = new("Boot");

            IGameLogger logger = factory.Create(in category);

            Assert.IsFalse(logger.IsEnabled(LogLevel.Debug));
            Assert.IsTrue(logger.IsEnabled(LogLevel.Info));
        }

        [Test]
        public void Log_BelowMinimum_IsDropped()
        {
            FakeLogSink sink = new();
            GameLoggerFactory factory = new(sink, new LogFilterConfig(LogLevel.Warning));
            LogCategory category = new("Boot");
            IGameLogger logger = factory.Create(in category);

            logger.Info("dropped");

            Assert.AreEqual(0, sink.Entries.Count);
        }

        [Test]
        public void Log_OffCategory_SilencesError()
        {
            FakeLogSink sink = new();
            Dictionary<string, LogLevel> overrides = new() { ["Boot"] = LogLevel.Off };
            GameLoggerFactory factory = new(sink, new LogFilterConfig(LogLevel.Info, overrides));
            LogCategory category = new("Boot");
            IGameLogger logger = factory.Create(in category);

            logger.Error("silenced", new InvalidOperationException("boom"));

            Assert.AreEqual(0, sink.Entries.Count);
        }
    }
}
