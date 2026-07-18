using NUnit.Framework;
using Didionysymus.Lattice.Runtime;
using Iterate.Application.Logging;
using Iterate.Composition.Root;
using Iterate.Infrastructure.Logging;

namespace Iterate.Composition.Root.Tests
{
    /// <summary>
    /// Headless composition tests: the project installer must yield a container that resolves
    /// the logging seam without any scene.
    /// </summary>
    public sealed class ProjectCompositionTests
    {
        [Test]
        public void Build_WithProjectInstaller_ResolvesLoggerFactory()
        {
            // Arrange
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            // Act
            IGameLoggerFactory factory = resolver.Resolve<IGameLoggerFactory>();
            LogCategory category = new("Test");
            IGameLogger logger = factory.Create(in category);

            // Assert
            Assert.IsNotNull(logger);
            Assert.AreEqual(category, logger.Category);

            resolver.Dispose();
        }

        [Test]
        public void Build_WithProjectInstaller_SinkIsConsoleSink()
        {
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            ILogSink sink = resolver.Resolve<ILogSink>();

            Assert.IsInstanceOf<ConsoleLogSink>(sink);

            resolver.Dispose();
        }
    }
}
