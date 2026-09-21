using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// Rebuilds a Session from a checkpoint: identities resolved against the live catalog, the Session
    /// itself rebuilt through its own validate-then-commit entry, and the recovery destination derived
    /// from the Process's authored role.
    ///
    /// Both revisions are checked before anything is resolved, because content that happens to still
    /// exist under a later catalog would restore a Session that is no longer the one that was saved.
    /// When a named definition does not resolve, the other categories are probed so that confusing a
    /// Dependency for an Instruction reports the confusion rather than claiming the content is missing.
    /// </summary>
    public static class CheckpointRestore
    {
        /// <summary>
        /// Restores a checkpoint against a catalog.
        /// </summary>
        /// <param name="state">The checkpoint to restore.</param>
        /// <param name="catalog">The frozen catalog to resolve content through.</param>
        /// <returns>The restore result.</returns>
        /// <exception cref="ArgumentException">Thrown when the checkpoint or catalog is null.</exception>
        public static CheckpointRestoreResult Restore(CheckpointState state, ContentCatalog catalog)
        {
            if (state == null)
                throw new ArgumentException("A restore requires a checkpoint.", nameof(state));

            if (catalog == null)
                throw new ArgumentException("A restore requires a catalog.", nameof(catalog));

            if (!string.Equals(state.CatalogRevision, catalog.Revision, StringComparison.Ordinal))
            {
                return Rejected(
                    CheckpointRestoreRejection.CatalogRevisionMismatch,
                    "the checkpoint was written against catalog revision '" + state.CatalogRevision + "'."
                );
            }

            if (!string.Equals(state.RandomServiceRevision, DeterminismService.RevisionIdentity, StringComparison.Ordinal))
            {
                return Rejected(
                    CheckpointRestoreRejection.RandomServiceRevisionMismatch,
                    "the checkpoint was written against random service revision '"
                        + state.RandomServiceRevision + "'."
                );
            }

            if (!catalog.TryGetProcessConfiguration(state.Process, out ProcessConfigurationDefinition configuration))
            {
                return Rejected(
                    CheckpointRestoreRejection.UnknownProcess,
                    "the catalog defines no Process '" + state.Process.Value + "'."
                );
            }

            CheckpointRecovery recovery;
            switch (configuration.Role)
            {
                case ProcessRole.Tutorial1:
                case ProcessRole.Tutorial2:
                    recovery = CheckpointRecovery.ScriptedRestart;
                    break;

                case ProcessRole.RouteProcess3:
                case ProcessRole.CriticalProcess:
                    recovery = CheckpointRecovery.BranchConfiguration;
                    break;

                default:
                    return Rejected(
                        CheckpointRestoreRejection.UnknownProcessRole,
                        "the Process '" + state.Process.Value + "' declares role '"
                            + configuration.Role + "', which has no recovery."
                    );
            }

            if (state.Route.HasValue && !catalog.TryGetRoute(state.Route.Value, out RouteDefinition _))
            {
                return Rejected(
                    CheckpointRestoreRejection.UnknownRoute,
                    "the catalog defines no route '" + state.Route.Value.Value + "'."
                );
            }

            List<RepositoryEntry> repository = new(state.Repository.Count);
            for (int index = 0; index < state.Repository.Count; index++)
            {
                CheckpointRepositoryItem item = state.Repository[index];
                if (!catalog.TryGetItem(item.Definition, out ContentDefinition definition))
                    return Unresolved(catalog, item.Definition, "a Repository item");

                RepositoryItem rebuilt = RepositoryItem.From(definition, item.Instance);
                if (item.Attachments.Count > 0 && rebuilt.Instruction == null)
                {
                    return Rejected(
                        CheckpointRestoreRejection.DefinitionCategoryMismatch,
                        "the checkpoint attaches a Patch to '" + item.Definition
                            + "', which is not an Instruction and has no sockets."
                    );
                }

                for (int socket = 0; socket < item.Attachments.Count; socket++)
                {
                    CheckpointAttachment attachment = item.Attachments[socket];
                    if (!TryGetPatch(catalog, attachment.Definition, out PatchDefinition patch))
                        return Unresolved(catalog, attachment.Definition, "an attached Patch");

                    rebuilt = rebuilt with
                    {
                        Instruction = rebuilt.Instruction.WithAttachment(new PatchAttachment(
                            attachment.Socket,
                            new PatchInstance(attachment.Instance, patch)
                        ))
                    };
                }

                repository.Add(new RepositoryEntry(rebuilt, item.Origin, item.StarterProtected, item.Suffix));
            }

            CheckpointEconomy economy = state.Economy;
            if (!TryGetDependency(catalog, economy.Starter.Definition, out DependencyDefinition starter))
                return Unresolved(catalog, economy.Starter.Definition, "the starter Dependency");

            List<InstalledDependency> installed = new(economy.Dependencies.Count);
            for (int index = 0; index < economy.Dependencies.Count; index++)
            {
                CheckpointDependency dependency = economy.Dependencies[index];
                if (!TryGetDependency(catalog, dependency.Definition, out DependencyDefinition definition))
                    return Unresolved(catalog, dependency.Definition, "an installed Dependency");

                installed.Add(new InstalledDependency(
                    new DependencyInstance(dependency.Instance, definition),
                    dependency.PricePaid,
                    dependency.Origin
                ));
            }

            List<UtilityCommitment> utilities = new(economy.Utilities.Count);
            for (int index = 0; index < economy.Utilities.Count; index++)
            {
                CheckpointUtility utility = economy.Utilities[index];
                if (!TryGetUtility(catalog, utility.Definition, out UtilityDefinition definition))
                    return Unresolved(catalog, utility.Definition, "a Utility commitment");

                utilities.Add(new UtilityCommitment(
                    utility.Instance,
                    definition,
                    utility.TargetProcess,
                    utility.OfferID
                ));
            }

            List<PatchGrant> grants = new(economy.Grants.Count);
            for (int index = 0; index < economy.Grants.Count; index++)
            {
                CheckpointGrant grant = economy.Grants[index];
                if (!TryGetPatch(catalog, grant.Definition, out PatchDefinition definition))
                    return Unresolved(catalog, grant.Definition, "a Patch grant");

                grants.Add(new PatchGrant(grant.Instance, definition, grant.Cached, grant.Basis));
            }

            EconomySnapshot economySnapshot = new(
                new TokenAmount(economy.Tokens),
                new DependencyInstance(economy.Starter.Instance, starter),
                economy.RAMCapacity,
                installed,
                utilities,
                grants,
                economy.TransactionCounters
            );

            SessionSnapshot snapshot = new(
                state.SystemIdentity,
                state.SessionIdentity,
                state.SessionSeedIdentity,
                state.CatalogRevision,
                state.LastAllocatedInstance,
                state.Ordinals,
                repository,
                state.Branch == null ? null : new ActiveBranchConfiguration(new List<InstanceID>(state.Branch)),
                economySnapshot
            );

            SessionRestoreResult session = SessionState.Restore(catalog, snapshot);
            if (!session.Succeeded)
            {
                return new CheckpointRestoreResult(
                    false,
                    null,
                    null,
                    CheckpointRestoreRejection.SessionRejected,
                    session.Rejection,
                    session.Detail
                );
            }

            return new CheckpointRestoreResult(
                true,
                session.Session,
                new CheckpointDestination(state.Process, state.Route, recovery),
                CheckpointRestoreRejection.None,
                SessionRestoreRejection.None,
                null
            );
        }

        /// <summary>
        /// Reports an unresolved identity, distinguishing content the catalog does not define from
        /// content it defines as another kind.
        /// </summary>
        /// <param name="catalog">The catalog to probe.</param>
        /// <param name="id">The identity that did not resolve.</param>
        /// <param name="position">What the checkpoint expected it to be.</param>
        /// <returns>The rejection.</returns>
        private static CheckpointRestoreResult Unresolved(ContentCatalog catalog, string id, string position)
        {
            string actual = CategoryOf(catalog, id);
            if (actual == null)
            {
                return Rejected(
                    CheckpointRestoreRejection.UnknownDefinition,
                    "the checkpoint names '" + id + "' as " + position + ", which the catalog does not define."
                );
            }

            return Rejected(
                CheckpointRestoreRejection.DefinitionCategoryMismatch,
                "the checkpoint names '" + id + "' as " + position
                    + ", but the catalog defines it as " + actual + "."
            );
        }

        /// <summary>
        /// The category the catalog defines an identity as, or null when it defines none.
        /// </summary>
        /// <param name="catalog">The catalog to probe.</param>
        /// <param name="id">The identity to look for.</param>
        /// <returns>The category name, or null.</returns>
        private static string CategoryOf(ContentCatalog catalog, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            if (catalog.TryGetItem(id, out ContentDefinition item))
                return item.Category.ToString();

            if (TryGetDependency(catalog, id, out DependencyDefinition _))
                return ContentCategory.Dependency.ToString();

            if (TryGetPatch(catalog, id, out PatchDefinition _))
                return ContentCategory.Patch.ToString();

            if (TryGetUtility(catalog, id, out UtilityDefinition _))
                return ContentCategory.Utility.ToString();

            return null;
        }

        /// <summary>
        /// Looks a Dependency up by its identity string, treating an empty identity as absent rather
        /// than letting the typed identity's own guard throw.
        /// </summary>
        /// <param name="catalog">The catalog to look in.</param>
        /// <param name="id">The identity string.</param>
        /// <param name="definition">The found definition, or null.</param>
        /// <returns>True when the identity resolves to a Dependency.</returns>
        private static bool TryGetDependency(ContentCatalog catalog, string id, out DependencyDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id))
                return false;

            return catalog.TryGetDependency(new DependencyID(id), out definition);
        }

        /// <summary>
        /// Looks a Patch up by its identity string.
        /// </summary>
        /// <param name="catalog">The catalog to look in.</param>
        /// <param name="id">The identity string.</param>
        /// <param name="definition">The found definition, or null.</param>
        /// <returns>True when the identity resolves to a Patch.</returns>
        private static bool TryGetPatch(ContentCatalog catalog, string id, out PatchDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id))
                return false;

            return catalog.TryGetPatch(new PatchID(id), out definition);
        }

        /// <summary>
        /// Looks a Utility up by its identity string.
        /// </summary>
        /// <param name="catalog">The catalog to look in.</param>
        /// <param name="id">The identity string.</param>
        /// <param name="definition">The found definition, or null.</param>
        /// <returns>True when the identity resolves to a Utility.</returns>
        private static bool TryGetUtility(ContentCatalog catalog, string id, out UtilityDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id))
                return false;

            return catalog.TryGetUtility(new UtilityID(id), out definition);
        }

        /// <summary>
        /// Builds a rejected result carrying neither a Session nor a destination.
        /// </summary>
        /// <param name="rejection">Why the checkpoint was refused.</param>
        /// <param name="detail">A sentence naming what was wrong.</param>
        /// <returns>The result.</returns>
        private static CheckpointRestoreResult Rejected(CheckpointRestoreRejection rejection, string detail)
        {
            return new CheckpointRestoreResult(
                false,
                null,
                null,
                rejection,
                SessionRestoreRejection.None,
                detail
            );
        }
    }
}