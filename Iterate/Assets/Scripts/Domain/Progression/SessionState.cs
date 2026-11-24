using System;
using System.Collections.Generic;
using System.Globalization;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Values;

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
                        $"The Starter Archetype names '{contentID}', which the catalog does not supply as a Repository item.",
                        nameof(archetype)
                    );

                repository.Acquire(definition, AcquisitionOrigin.Starter);
            }

            if (!catalog.TryGetDependency(archetype.StarterDependency, out DependencyDefinition dependency))
                throw new ArgumentException(
                    $"The Starter Archetype names starter Dependency '{archetype.StarterDependency.Value}', which the catalog does not define.",
                    nameof(archetype)
                );

            if (dependency.RAM != 0)
                throw new ArgumentException(
                    $"The starter Dependency '{dependency.ID.Value}' consumes RAM, which a starter Dependency may not.",
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
        
        /// <summary>
        /// Takes an immutable view of everything the Session carries between Processes.
        /// </summary>
        /// <returns>The snapshot.</returns>
        public SessionSnapshot Snapshot()
        {
            EconomySnapshot economy = new(
                Economy.Tokens.Balance,
                Economy.Dependencies.Starter,
                Economy.Dependencies.Capacity,
                new List<InstalledDependency>(Economy.Dependencies.Installed),
                new List<UtilityCommitment>(Economy.Utilities),
                new List<PatchGrant>(Economy.PatchGrants),
                Economy.TransactionCounters
            );

            return new SessionSnapshot(
                SystemIdentity,
                SessionIdentity,
                SessionSeedIdentity,
                CatalogRevision,
                InstanceIDs.LastAllocated,
                Ordinals.Snapshot(),
                Repository.Snapshot().Entries,
                ActiveBranch.Confirmed,
                economy
            );
        }

        /// <summary>
        /// Rebuilds a Session from a snapshot. Every cross-holder rule is checked before anything is
        /// built, so a rejected snapshot leaves no Session behind at all: the holders themselves assume
        /// a validated input, and this is the single place that validation happens.
        /// </summary>
        /// <param name="catalog">The frozen catalog the Session must match.</param>
        /// <param name="snapshot">The snapshot to rebuild from.</param>
        /// <returns>The restored Session, or a typed rejection carrying no Session.</returns>
        public static SessionRestoreResult Restore(ContentCatalog catalog, SessionSnapshot snapshot)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            SessionRestoreRejection rejection = Validate(catalog, snapshot, out string detail);
            if (rejection != SessionRestoreRejection.None)
                return SessionRestoreResult.Rejected(rejection, detail);

            EconomySnapshot economy = snapshot.Economy;
            DependencyRack rack = new(economy.Starter, economy.RAMCapacity);
            for (int index = 0; index < economy.Installed.Count; index++)
            {
                InstalledDependency installed = economy.Installed[index];
                RackResult installation = rack.Install(
                    installed.Instance.Definition,
                    installed.Instance.InstanceID,
                    installed.PricePaid,
                    installed.Origin
                );

                if (!installation.Succeeded)
                {
                    return SessionRestoreResult.Rejected(
                        SessionRestoreRejection.RAMOverCapacity,
                        $"the installed Dependencies need more RAM than the capacity of {economy.RAMCapacity.ToString(CultureInfo.InvariantCulture)}."
                    );
                }
            }

            InstanceIDSource instanceIDs = InstanceIDSource.Restore(snapshot.LastAllocatedInstance);
            return SessionRestoreResult.Restored(new SessionState(
                Repository.Restore(instanceIDs, snapshot.Repository),
                ActiveBranch.Restore(snapshot.ConfirmedBranch),
                EconomyState.Restore(
                    instanceIDs,
                    catalog.Parameters,
                    new TokenLedger(economy.Tokens),
                    rack,
                    economy.Utilities,
                    economy.Grants,
                    economy.TransactionCounters
                ),
                instanceIDs,
                OccurrenceOrdinalTracker.Restore(snapshot.Ordinals),
                snapshot.SystemIdentity,
                snapshot.SessionIdentity,
                snapshot.SessionSeedIdentity,
                snapshot.CatalogRevision
            ));
        }

        /// <summary>
        /// Checks every rule a snapshot must satisfy before a Session is built from it, in the order a
        /// reader of the rejection would want them: what the snapshot is, then what it counts, then what
        /// it names, then what those names imply.
        /// </summary>
        /// <param name="catalog">The catalog the snapshot must match.</param>
        /// <param name="snapshot">The snapshot to check.</param>
        /// <param name="detail">A sentence naming what was wrong, or null.</param>
        /// <returns>The rejection, or None when the snapshot is sound.</returns>
        private static SessionRestoreRejection Validate(
            ContentCatalog catalog,
            SessionSnapshot snapshot,
            out string detail
        )
        {
            if (!string.Equals(snapshot.CatalogRevision, catalog.Revision, StringComparison.Ordinal))
            {
                detail = $"the snapshot names catalog revision '{snapshot.CatalogRevision}', not '{catalog.Revision}'.";
                return SessionRestoreRejection.CatalogRevisionMismatch;
            }

            SessionRestoreRejection rejection = ValidateOrdinals(snapshot.Ordinals, out detail);
            if (rejection != SessionRestoreRejection.None)
                return rejection;

            rejection = ValidateCounters(snapshot.Economy.TransactionCounters, out detail);
            if (rejection != SessionRestoreRejection.None)
                return rejection;

            rejection = ValidateInstances(snapshot, out detail);
            if (rejection != SessionRestoreRejection.None)
                return rejection;

            rejection = ValidateSuffixes(snapshot.Repository, out detail);
            if (rejection != SessionRestoreRejection.None)
                return rejection;

            rejection = ValidateBranch(snapshot, out detail);
            if (rejection != SessionRestoreRejection.None)
                return rejection;

            if (snapshot.Economy.Starter.Definition.RAM != 0)
            {
                detail = $"the starter Dependency '{snapshot.Economy.Starter.Definition.ID.Value}' consumes RAM, which a starter Dependency may not.";
                return SessionRestoreRejection.StarterConsumesRAM;
            }

            return ValidateUtilities(snapshot.Economy.Utilities, out detail);
        }

        /// <summary>
        /// Checks the occurrence-ordinal counters for a malformed pair, a count below one or a repeat.
        /// </summary>
        /// <param name="ordinals">The counters to check.</param>
        /// <param name="detail">A sentence naming what was wrong, or null.</param>
        /// <returns>The rejection, or None.</returns>
        private static SessionRestoreRejection ValidateOrdinals(
            IReadOnlyList<OrdinalCounter> ordinals,
            out string detail
        )
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            for (int index = 0; index < ordinals.Count; index++)
            {
                OrdinalCounter counter = ordinals[index];
                if (counter == null
                    || string.IsNullOrEmpty(counter.SelectionPurpose)
                    || string.IsNullOrEmpty(counter.GoverningContextIdentity)
                    || counter.Count < 1)
                {
                    detail = "an occurrence-ordinal counter is malformed.";
                    return SessionRestoreRejection.OrdinalCounterInvalid;
                }

                if (!seen.Add($"{counter.SelectionPurpose}�{counter.GoverningContextIdentity}"))
                {
                    detail = $"the occurrence-ordinal counters repeat the pair ('{counter.SelectionPurpose}', '{counter.GoverningContextIdentity}').";
                    return SessionRestoreRejection.OrdinalCounterInvalid;
                }
            }

            detail = null;
            return SessionRestoreRejection.None;
        }

        /// <summary>
        /// Checks the transaction counters for a count below one or a repeated kind.
        /// </summary>
        /// <param name="counters">The counters to check.</param>
        /// <param name="detail">A sentence naming what was wrong, or null.</param>
        /// <returns>The rejection, or None.</returns>
        private static SessionRestoreRejection ValidateCounters(
            IReadOnlyList<TransactionCounter> counters,
            out string detail
        )
        {
            HashSet<TransactionKind> seen = new();
            for (int index = 0; index < counters.Count; index++)
            {
                TransactionCounter counter = counters[index];
                if (counter == null || counter.Count < 1)
                {
                    detail = "a transaction counter is below one.";
                    return SessionRestoreRejection.TransactionCounterInvalid;
                }

                if (!seen.Add(counter.Kind))
                {
                    detail = $"the transaction counters repeat the kind {counter.Kind}.";
                    return SessionRestoreRejection.TransactionCounterInvalid;
                }
            }

            detail = null;
            return SessionRestoreRejection.None;
        }

        /// <summary>
        /// Checks every position carrying an instance identity — Repository items and the Patches
        /// attached to them, the starter and installed Dependencies, commitments and grants — against
        /// the allocation counter and against each other. A single sweep, because an identity above the
        /// counter in any one of them would collide with the Session's next allocation.
        /// </summary>
        /// <param name="snapshot">The snapshot to check.</param>
        /// <param name="detail">A sentence naming what was wrong, or null.</param>
        /// <returns>The rejection, or None.</returns>
        private static SessionRestoreRejection ValidateInstances(SessionSnapshot snapshot, out string detail)
        {
            HashSet<InstanceID> seen = new();
            EconomySnapshot economy = snapshot.Economy;

            for (int index = 0; index < snapshot.Repository.Count; index++)
            {
                RepositoryItem item = snapshot.Repository[index].Item;
                SessionRestoreRejection rejection = Account(item.InstanceID, snapshot, seen, "a Repository item", out detail);
                if (rejection != SessionRestoreRejection.None)
                    return rejection;

                if (item.Instruction == null)
                    continue;

                IReadOnlyList<PatchAttachment> attachments = item.Instruction.AttachedPatches;
                for (int socket = 0; socket < attachments.Count; socket++)
                {
                    rejection = Account(attachments[socket].Patch.InstanceID, snapshot, seen, "an attached Patch", out detail);
                    if (rejection != SessionRestoreRejection.None)
                        return rejection;
                }
            }

            SessionRestoreRejection starter = Account(economy.Starter.InstanceID, snapshot, seen, "the starter Dependency", out detail);
            if (starter != SessionRestoreRejection.None)
                return starter;

            for (int index = 0; index < economy.Installed.Count; index++)
            {
                SessionRestoreRejection rejection = Account(
                    economy.Installed[index].Instance.InstanceID,
                    snapshot,
                    seen,
                    "an installed Dependency",
                    out detail
                );

                if (rejection != SessionRestoreRejection.None)
                    return rejection;
            }

            for (int index = 0; index < economy.Utilities.Count; index++)
            {
                SessionRestoreRejection rejection = Account(
                    economy.Utilities[index].Identity,
                    snapshot,
                    seen,
                    "a Utility commitment",
                    out detail
                );

                if (rejection != SessionRestoreRejection.None)
                    return rejection;
            }

            for (int index = 0; index < economy.Grants.Count; index++)
            {
                SessionRestoreRejection rejection = Account(
                    economy.Grants[index].GrantIdentity,
                    snapshot,
                    seen,
                    "a Patch grant",
                    out detail
                );

                if (rejection != SessionRestoreRejection.None)
                    return rejection;
            }

            detail = null;
            return SessionRestoreRejection.None;
        }

        /// <summary>
        /// Accounts for one instance identity: above the counter, already seen, or sound.
        /// </summary>
        /// <param name="id">The identity to account for.</param>
        /// <param name="snapshot">The snapshot the counter comes from.</param>
        /// <param name="seen">The identities already accounted for.</param>
        /// <param name="position">What holds the identity, for the rejection detail.</param>
        /// <param name="detail">A sentence naming what was wrong, or null.</param>
        /// <returns>The rejection, or None.</returns>
        private static SessionRestoreRejection Account(
            InstanceID id,
            SessionSnapshot snapshot,
            HashSet<InstanceID> seen,
            string position,
            out string detail
        )
        {
            if (id.Value > snapshot.LastAllocatedInstance)
            {
                detail = $"{position} carries {id}, above the last allocated identity #{snapshot.LastAllocatedInstance.ToString(CultureInfo.InvariantCulture)}.";
                return SessionRestoreRejection.InstanceAboveCounter;
            }

            if (!seen.Add(id))
            {
                detail = $"{position} repeats {id}.";
                return SessionRestoreRejection.DuplicateInstance;
            }

            detail = null;
            return SessionRestoreRejection.None;
        }

        /// <summary>
        /// Checks every Repository entry carries an acquisition suffix of at least one.
        /// </summary>
        /// <param name="entries">The entries to check.</param>
        /// <param name="detail">A sentence naming what was wrong, or null.</param>
        /// <returns>The rejection, or None.</returns>
        private static SessionRestoreRejection ValidateSuffixes(
            IReadOnlyList<RepositoryEntry> entries,
            out string detail
        )
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].Suffix >= 1)
                    continue;

                detail = $"the Repository entry {entries[index].Item.InstanceID} carries suffix {entries[index].Suffix.ToString(CultureInfo.InvariantCulture)}.";
                return SessionRestoreRejection.SuffixInvalid;
            }

            detail = null;
            return SessionRestoreRejection.None;
        }

        /// <summary>
        /// Checks the confirmed Branch names only instances the Repository holds.
        /// </summary>
        /// <param name="snapshot">The snapshot to check.</param>
        /// <param name="detail">A sentence naming what was wrong, or null.</param>
        /// <returns>The rejection, or None.</returns>
        private static SessionRestoreRejection ValidateBranch(SessionSnapshot snapshot, out string detail)
        {
            detail = null;
            if (snapshot.ConfirmedBranch == null)
                return SessionRestoreRejection.None;

            HashSet<InstanceID> held = new();
            for (int index = 0; index < snapshot.Repository.Count; index++)
            {
                held.Add(snapshot.Repository[index].Item.InstanceID);
            }

            IReadOnlyList<InstanceID> eligible = snapshot.ConfirmedBranch.Eligible;
            for (int index = 0; index < eligible.Count; index++)
            {
                if (held.Contains(eligible[index]))
                    continue;

                detail = $"the confirmed Branch names {eligible[index]}, which the Repository does not hold.";
                return SessionRestoreRejection.BranchMemberMissing;
            }

            return SessionRestoreRejection.None;
        }

        /// <summary>
        /// Checks no Utility is committed twice to one Process. The same Utility on two Processes is
        /// legal and stays so.
        /// </summary>
        /// <param name="utilities">The commitments to check.</param>
        /// <param name="detail">A sentence naming what was wrong, or null.</param>
        /// <returns>The rejection, or None.</returns>
        private static SessionRestoreRejection ValidateUtilities(
            IReadOnlyList<UtilityCommitment> utilities,
            out string detail
        )
        {
            for (int index = 0; index < utilities.Count; index++)
            {
                for (int other = index + 1; other < utilities.Count; other++)
                {
                    if (utilities[index].Definition.ID != utilities[other].Definition.ID
                        || utilities[index].TargetProcess != utilities[other].TargetProcess)
                    {
                        continue;
                    }

                    detail = $"the Utility '{utilities[index].Definition.ID.Value}' is committed twice to {utilities[index].TargetProcess.Value}.";
                    return SessionRestoreRejection.DuplicateUtilityCommitment;
                }
            }

            detail = null;
            return SessionRestoreRejection.None;
        }
    }
}