using Didionysymus.Lattice.Runtime;
using Iterate.Application.Logging;
using Iterate.Infrastructure.Logging;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// Registers the Session-independent services owned by the project scope. Currently the
    /// logging seam: the filter configuration, the single console sink, and the logger factory.
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
        }
    }
}