using System.IO;
using Didionysymus.Lattice.Runtime;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Application.Logging;
using Iterate.Infrastructure.Content;
using Iterate.Infrastructure.Logging;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// Registers the Session-independent services owned by the project scope: the logging seam and the
    /// catalog pipeline (reader, validator, freezer, holder, loader, the StreamingAssets directory
    /// source, and the boot-time async-startable adapter).
    /// </summary>
    public sealed class ProjectInstaller : IInstaller
    {
        /// <summary>
        /// Registers the project-scope services on the builder.
        /// </summary>
        /// <param name="builder">The container builder to register into.</param>
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(new LogFilterConfig(LogLevel.Info));
            builder.Register<ILogSink, ConsoleLogSink>(Lifetime.Singleton);
            builder.Register<IGameLoggerFactory, GameLoggerFactory>(Lifetime.Singleton);

            builder.Register<CatalogJsonReader>(Lifetime.Singleton);
            builder.Register<ICatalogValidator, CatalogValidator>(Lifetime.Singleton);
            builder.Register<CatalogFreezer>(Lifetime.Singleton);
            builder.Register<CatalogHolder>(Lifetime.Singleton);
            builder.Register<CatalogLoader>(Lifetime.Singleton);
            builder.RegisterFactory<ICatalogFileSource>(
                static resolver => new CatalogDirectorySource(Path.Combine(UnityEngine.Application.streamingAssetsPath, "Catalog")),
                Lifetime.Singleton
            );
            builder.Register<IAsyncStartable, CatalogAsyncStartable>(Lifetime.Singleton);
        }
    }
}