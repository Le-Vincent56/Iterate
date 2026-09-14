using System;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The Session-scoped state a run carries from start to finish: the Repository, the Session's economy of Tokens 
    /// and RAM, the deterministic identity source and ordinal tracker every Session-scoped caller shares, and the
    /// identities that stamp the Session's decisions. Built in one step from a Starter Archetype, so a Session
    /// always begins in a legal state or not at all.
    /// </summary>
    public sealed class SessionState
    {
        /// <summary>
        /// The Session's Repository of unique item instances.
        /// </summary>
        public Repository Repository { get; }
        
        /// <summary>
        /// The Session's Active Branch. A Session begins with no confirmed Branch and no configuration
        /// open; the first configuration is the onboarding Branch screen before Process 3.
        /// </summary>
        public ActiveBranch ActiveBranch { get; }

        /// <summary>
        /// The Session's economy: Tokens, RAM and the installed Dependencies.
        /// </summary>
        public EconomyState Economy { get; }

        /// <summary>
        /// The Dependency installed at Session start. Forwards to the rack, which is where a
        /// Dependency's installed state actually lives.
        /// </summary>
        public DependencyInstance StarterDependency => Economy.Dependencies.Starter;

        /// <summary>
        /// The identity source every Session-scoped allocation draws from.
        /// </summary>
        public InstanceIDSource InstanceIDs { get; }

        /// <summary>
        /// The Session-scoped occurrence-ordinal tracker. Exposure draw positions are deliberately not
        /// tracked here: a draw position is Process-scoped, so the draw owns its own counter and an
        /// in-Session retry restarts from the first position with no reset machinery.
        /// </summary>
        public OccurrenceOrdinalTracker Ordinals { get; }

        /// <summary>
        /// The identity of the System this Session is playing.
        /// </summary>
        public string SystemIdentity { get; }

        /// <summary>
        /// The Session's own identity.
        /// </summary>
        public string SessionIdentity { get; }

        /// <summary>
        /// The seed identity every Session decision is derived from.
        /// </summary>
        public string SessionSeedIdentity { get; }

        /// <summary>
        /// The revision of the catalog this Session was built against.
        /// </summary>
        public string CatalogRevision { get; }

        private SessionState(
            Repository repository,
            ActiveBranch activeBranch,
            EconomyState economy,
            InstanceIDSource instanceIDs,
            OccurrenceOrdinalTracker ordinals,
            string systemIdentity,
            string sessionIdentity,
            string sessionSeedIdentity,
            string catalogRevision
        )
        {
            Repository = repository;
            ActiveBranch = activeBranch;
            Economy = economy;
            InstanceIDs = instanceIDs;
            Ordinals = ordinals;
            SystemIdentity = systemIdentity;
            SessionIdentity = sessionIdentity;
            SessionSeedIdentity = sessionSeedIdentity;
            CatalogRevision = catalogRevision;
        }

        /// <summary>
        /// Builds a Session from a Starter Archetype: the Repository is seeded in the archetype's
        /// authored order, every starter entry protected, and the starter Dependency installed. A
        /// starting entry that does not resolve to a Repository item, or a starter Dependency that
        /// consumes RAM, is a content error and throws rather than producing a half-built Session.
        /// </summary>
        /// <param name="catalog">The frozen catalog to resolve content through.</param>
        /// <param name="archetype">The Starter Archetype to seed from.</param>
        /// <param name="systemIdentity">The System this Session plays.</param>
        /// <param name="sessionIdentity">The Session's own identity.</param>
        /// <param name="sessionSeedIdentity">The Session's seed identity.</param>
        /// <returns>The seeded Session state.</returns>
        /// <exception cref="ArgumentException">Thrown when the archetype names content the catalog cannot supply as an item, or a RAM-consuming starter Dependency.</exception>
        public static SessionState Create(
            ContentCatalog catalog,
            StarterArchetypeDefinition archetype,
            string systemIdentity,
            string sessionIdentity,
            string sessionSeedIdentity
        )
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            if (archetype == null)
                throw new ArgumentNullException(nameof(archetype));

            InstanceIDSource instanceIDs = new();
            Repository repository = new(instanceIDs);

            foreach (string contentID in archetype.StartingRepository)
            {
                if (!catalog.TryGetItem(contentID, out ContentDefinition definition))
                    throw new ArgumentException(
                        "The Starter Archetype names '" + contentID + "', which the catalog does not supply as a Repository item.",
                        nameof(archetype)
                    );

                repository.Acquire(definition, AcquisitionOrigin.Starter);
            }

            if (!catalog.TryGetDependency(archetype.StarterDependency, out DependencyDefinition dependency))
                throw new ArgumentException(
                    "The Starter Archetype names starter Dependency '" + archetype.StarterDependency.Value + "', which the catalog does not define.",
                    nameof(archetype)
                );

            if (dependency.RAM != 0)
                throw new ArgumentException(
                    "The starter Dependency '" + dependency.ID.Value + "' consumes RAM, which a starter Dependency may not.",
                    nameof(archetype)
                );

            DependencyInstance starterDependency = new(instanceIDs.Next(), dependency);

            return new SessionState(
                repository,
                new ActiveBranch(),
                new EconomyState(instanceIDs, starterDependency, catalog.Parameters),
                instanceIDs,
                new OccurrenceOrdinalTracker(),
                systemIdentity,
                sessionIdentity,
                sessionSeedIdentity,
                catalog.Revision
            );
        }
    }
}