using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Logging;

namespace Iterate.Application.Logging.Tests
{
    /// <summary>
    /// Tests default-minimum and per-category override resolution of <see cref="LogFilterConfig"/>.
    /// </summary>
    public sealed class LogFilterConfigTests
    {
        [Test]
        public void MinimumLevelFor_NoOverride_ReturnsDefault()
        {
            LogFilterConfig config = new(LogLevel.Info);
            LogCategory category = new("Boot");

            Assert.AreEqual(LogLevel.Info, config.MinimumLevelFor(in category));
        }

        [Test]
        public void MinimumLevelFor_WithOverride_ReturnsOverride()
        {
            Dictionary<string, LogLevel> overrides = new() { ["Playback"] = LogLevel.Debug };
            LogFilterConfig config = new(LogLevel.Info, overrides);
            LogCategory category = new("Playback");

            Assert.AreEqual(LogLevel.Debug, config.MinimumLevelFor(in category));
        }

        [Test]
        public void MinimumLevelFor_OffOverride_ReturnsOff()
        {
            Dictionary<string, LogLevel> overrides = new() { ["Noise"] = LogLevel.Off };
            LogFilterConfig config = new(LogLevel.Info, overrides);
            LogCategory category = new("Noise");

            Assert.AreEqual(LogLevel.Off, config.MinimumLevelFor(in category));
        }

        [Test]
        public void Constructor_NullOverrides_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new LogFilterConfig(LogLevel.Info, null));
        }
    }
}
