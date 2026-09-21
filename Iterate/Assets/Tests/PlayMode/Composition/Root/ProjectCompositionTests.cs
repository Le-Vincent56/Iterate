using NUnit.Framework;
using Didionysymus.Lattice.Runtime;
using Iterate.Application.Logging;
using Iterate.Application.Persistence;
using Iterate.Composition.Root;
using Iterate.Infrastructure.Logging;
using Iterate.Infrastructure.Persistence;

namespace Iterate.Composition.Root.Tests
{
    /// <summary>
    /// Headless composition tests: the project installer must yield a container that resolves
    /// the logging seam and the persistence seam without any scene.
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

        [Test]
        public void Build_WithProjectInstaller_ResolvesPersistenceService()
        {
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            PersistenceService service = resolver.Resolve<PersistenceService>();

            Assert.IsNotNull(service);

            resolver.Dispose();
        }

        [Test]
        public void Build_WithProjectInstaller_SaveStoreIsFileSaveStore()
        {
            // The adapter is bound by factory over the platform persistent-data path, which only exists
            // inside the player — so this is the one place the real binding can be proved at all.
            IObjectResolver resolver = Container.Build(builder => new ProjectInstaller().Install(builder));

            ISaveFileStore store = resolver.Resolve<ISaveFileStore>();

            Assert.IsInstanceOf<FileSaveStore>(store);

            resolver.Dispose();
        }
    }
}
