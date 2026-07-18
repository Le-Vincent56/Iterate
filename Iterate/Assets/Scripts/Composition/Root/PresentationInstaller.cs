using Didionysymus.Lattice.Runtime;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// Registers Presentation-facing services: view facades, playback, and input readers will
    /// land here as Presentation features arrive. Deliberately empty until then.
    /// </summary>
    public sealed class PresentationInstaller : IInstaller
    {
        /// <summary>
        /// Registers the Presentation-facing services on the builder.
        /// </summary>
        /// <param name="builder">The container builder to register into.</param>
        public void Install(IContainerBuilder builder)
        {
        }
    }
}