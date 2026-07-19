using Didionysymus.Lattice.Runtime;
using Didionysymus.Lattice.Runtime.Unity;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// The composition root: lives on the ProjectScope GameObject in the persistent scene, owns
    /// the root resolver for the application's lifetime, and installs the project and
    /// presentation installers. Child scopes for Sessions and Processes are created from this
    /// scope's resolver, never by additional scene scopes. After the base scene walk and Phase 2,
    /// it kicks the fault-contained async-startable phase that loads the catalog.
    /// </summary>
    public sealed class ProjectLifetimeScope : LifetimeScopeBehaviour
    {
        /// <summary>
        /// Installs the project-level registrations into the root container.
        /// </summary>
        /// <param name="builder">The container builder for the root scope.</param>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Install(new ProjectInstaller());
            builder.Install(new PresentationInstaller());
        }

        /// <summary>
        /// Runs the base scene walk and Phase 2, then kicks the async boot phase. The base call must
        /// come first so injected fields are populated before any startable runs; the boot runner is
        /// fire-and-forget and contains its own faults.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            _ = CatalogBootRunner.RunAsync(Resolver, destroyCancellationToken);
        }
    }
}