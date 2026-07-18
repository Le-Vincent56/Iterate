using Didionysymus.Lattice.Runtime;
using Didionysymus.Lattice.Runtime.Unity;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// The composition root: lives on the ProjectScope GameObject in the persistent scene, owns
    /// the root resolver for the application's lifetime, and installs the project and
    /// presentation installers. Child scopes for Sessions and Processes are created from this
    /// scope's resolver, never by additional scene scopes.
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
    }
}