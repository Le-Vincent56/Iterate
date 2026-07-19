using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Didionysymus.Lattice.Runtime;
using Iterate.Application.Content;
using Iterate.Application.Logging;

namespace Iterate.Composition.Root.Tests
{
    /// <summary>
    /// The stranded-Unloaded regression: a throwing <see cref="IAsyncStartable"/> ahead of the catalog
    /// must drive the holder to Failed via <see cref="CatalogHolder.TryMarkFailed"/> and log one Error,
    /// never leaving the holder Unloaded. Exercises the production <see cref="CatalogBootRunner"/>
    /// against a headless container.
    /// </summary>
    public sealed class CatalogBootRunnerFaultTests
    {
        private IObjectResolver _resolver;

        [TearDown]
        public void TearDown()
        {
            _resolver?.Dispose();
        }

        [Test]
        public void RunAsync_ThrowingStartable_DrivesHolderToFailedWithRunnerFault()
        {
            CapturingLogSink sink = new();
            _resolver = Container.Build(builder =>
            {
                builder.RegisterInstance(new LogFilterConfig(LogLevel.Info));
                builder.RegisterInstance<ILogSink>(sink);
                builder.Register<IGameLoggerFactory, GameLoggerFactory>(Lifetime.Singleton);
                builder.Register<CatalogHolder>(Lifetime.Singleton);
                builder.Register<IAsyncStartable, ThrowingStartable>(Lifetime.Singleton);
            });

            // Task.Run keeps the sync-over-async wait off the test's synchronization context.
            Task.Run(() => CatalogBootRunner.RunAsync(_resolver, CancellationToken.None))
                .GetAwaiter().GetResult();

            CatalogHolder holder = _resolver.Resolve<CatalogHolder>();
            Assert.AreEqual(CatalogState.Failed, holder.State);
            Assert.GreaterOrEqual(holder.Errors.Count, 1);
            Assert.AreEqual("boot.runner-fault", holder.Errors[0].RuleName);
            Assert.IsTrue(sink.HasError());
        }

        /// <summary>
        /// An <see cref="IAsyncStartable"/> that always faults synchronously, standing in for a future
        /// startable that throws before the catalog's turn.
        /// </summary>
        private sealed class ThrowingStartable : IAsyncStartable
        {
            public Task StartAsync(CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("boot startable failed.");
            }
        }
    }
}
