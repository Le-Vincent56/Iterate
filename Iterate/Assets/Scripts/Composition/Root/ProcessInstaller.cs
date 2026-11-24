using System;
using Didionysymus.Lattice.Runtime;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// Registers the holders a Process scope serves: the Process and its Build, as the instances it was
    /// opened around.
    /// </summary>
    public sealed class ProcessInstaller : IInstaller
    {
        private readonly ProcessState _process;
        private readonly BuildState _build;

        /// <summary>
        /// Creates the installer for one Process.
        /// </summary>
        /// <param name="process">The Process.</param>
        /// <param name="build">The Process's Build.</param>
        /// <exception cref="ArgumentNullException">Thrown when either holder is null.</exception>
        public ProcessInstaller(ProcessState process, BuildState build)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _build = build ?? throw new ArgumentNullException(nameof(build));
        }

        /// <summary>
        /// Registers the Process and its Build.
        /// </summary>
        /// <param name="builder">The Process scope's builder.</param>
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_process);
            builder.RegisterInstance(_build);
        }
    }
}