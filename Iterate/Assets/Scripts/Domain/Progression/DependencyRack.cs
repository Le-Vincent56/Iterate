using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The Session's installed Dependencies and its RAM. RAM is reserved for as long as a Dependency
    /// is installed and released when it is destroyed, so capacity is an occupancy rather than a
    /// running total. The starter Dependency is installed outside the capacity — it consumes no RAM
    /// (SessionState.Create enforces that at seeding) and can never be destroyed — so it is held
    /// separately rather than as an entry a caller could reach with Destroy.
    /// </summary>
    public sealed class DependencyRack
    {
        private readonly List<InstalledDependency> _installed = new();

        /// <summary>
        /// The permanently installed starter Dependency.
        /// </summary>
        public DependencyInstance Starter { get; }

        /// <summary>
        /// The removable installations, in installation order.
        /// </summary>
        public IReadOnlyList<InstalledDependency> Installed => _installed;

        /// <summary>
        /// The RAM available to installations.
        /// </summary>
        public int Capacity { get; private set; }

        /// <summary>
        /// The RAM currently reserved.
        /// </summary>
        public int Usage { get; private set; }

        /// <summary>
        /// The RAM still free.
        /// </summary>
        public int Free => Capacity - Usage;

        /// <summary>
        /// Every Dependency in effect, starter first. Rebuilt on each read: it is read once per
        /// execution request and once per Process creation, which does not earn cached state.
        /// </summary>
        public IReadOnlyList<DependencyInstance> AllInstalled
        {
            get
            {
                List<DependencyInstance> all = new List<DependencyInstance>(_installed.Count + 1) { Starter };
                for (int i = 0; i < _installed.Count; i++)
                {
                    all.Add(_installed[i].Instance);
                }

                return all;
            }
        }

        public DependencyRack(DependencyInstance starter, int capacity)
        {
            Starter = starter ?? throw new ArgumentException("A rack requires a starter Dependency.", nameof(starter));
            Capacity = capacity;
        }

        /// <summary>
        /// Finds a removable installation by identity. The starter is deliberately not found: it is
        /// not a removable entry, and every caller of this is about to act on what it finds.
        /// </summary>
        /// <param name="id">The instance identity to look for.</param>
        /// <param name="entry">The installation found, or null.</param>
        /// <returns>True when a removable installation carries that identity.</returns>
        public bool TryGet(InstanceID id, out InstalledDependency entry)
        {
            for (int i = 0; i < _installed.Count; i++)
            {
                if (_installed[i].Instance.InstanceID == id)
                {
                    entry = _installed[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Whether an identity names a Dependency this Session may destroy.
        /// </summary>
        /// <param name="id">The instance identity to test.</param>
        /// <returns>True when it is installed and not the starter.</returns>
        public bool IsRemovable(InstanceID id)
        {
            return TryGet(id, out InstalledDependency _);
        }

        /// <summary>
        /// Installs a Dependency, reserving its RAM.
        /// </summary>
        /// <param name="definition">The frozen Dependency definition.</param>
        /// <param name="id">The instance identity to give it.</param>
        /// <param name="pricePaid">The Tokens paid for it.</param>
        /// <param name="origin">How it was acquired.</param>
        /// <returns>The rack result.</returns>
        public RackResult Install(
            DependencyDefinition definition,
            InstanceID id,
            int pricePaid,
            DependencyOrigin origin
        )
        {
            if (definition == null)
                throw new ArgumentException("An installation requires a definition.", nameof(definition));

            if (definition.RAM > Free)
                return new RackResult(false, DependencyRejection.InsufficientRAM, null, 0);

            InstalledDependency entry = new(new DependencyInstance(id, definition), pricePaid, origin);
            _installed.Add(entry);
            Usage += definition.RAM;
            return new RackResult(true, DependencyRejection.None, entry, definition.RAM);
        }

        /// <summary>
        /// Destroys an installed Dependency, releasing its RAM.
        /// </summary>
        /// <param name="id">The instance identity to destroy.</param>
        /// <returns>The rack result.</returns>
        public RackResult Destroy(InstanceID id)
        {
            if (Starter.InstanceID == id)
                return new RackResult(false, DependencyRejection.StarterPermanent, null, 0);

            for (int i = 0; i < _installed.Count; i++)
            {
                if (_installed[i].Instance.InstanceID != id)
                    continue;

                InstalledDependency entry = _installed[i];
                _installed.RemoveAt(i);
                Usage -= entry.Instance.Definition.RAM;
                return new RackResult(true, DependencyRejection.None, entry, entry.Instance.Definition.RAM);
            }

            return new RackResult(false, DependencyRejection.UnknownInstance, null, 0);
        }

        /// <summary>
        /// Sets the RAM capacity. A capacity below what is already reserved is refused rather than
        /// evicting an installation, because nothing in the economy destroys a Dependency implicitly.
        /// </summary>
        /// <param name="capacity">The new capacity.</param>
        /// <returns>The rack result.</returns>
        public RackResult SetCapacity(int capacity)
        {
            if (capacity < Usage)
                return new RackResult(false, DependencyRejection.CapacityBelowUsage, null, 0);

            Capacity = capacity;
            return new RackResult(true, DependencyRejection.None, null, 0);
        }
    }
}