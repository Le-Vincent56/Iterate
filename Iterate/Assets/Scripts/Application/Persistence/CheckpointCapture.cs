using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// Flattens a Session into a checkpoint: identities and definition IDs rather than the content
    /// itself, so what is saved stays readable without the object graph that produced it.
    ///
    /// A Session with a Branch configuration open is refused rather than captured. A draft is a
    /// half-made decision, and a checkpoint that preserved one would restore the player into an edit
    /// they never finished — so the capture moment is always between decisions, never inside one.
    /// </summary>
    public static class CheckpointCapture
    {
        /// <summary>
        /// Captures a Session at a Process entry.
        /// </summary>
        /// <param name="session">The Session to capture.</param>
        /// <param name="process">The Process the player is entering.</param>
        /// <param name="route">The route taken to it, or null.</param>
        /// <param name="consecutiveFailures">How many times this checkpoint has been failed in a row.</param>
        /// <returns>The capture result.</returns>
        /// <exception cref="ArgumentException">Thrown when the Session is null or the failure count is negative.</exception>
        public static CheckpointCaptureResult Capture(
            SessionState session,
            ProcessID process,
            RouteID? route,
            int consecutiveFailures
        )
        {
            if (session == null)
                throw new ArgumentException("A capture requires a Session.", nameof(session));

            if (consecutiveFailures < 0)
                throw new ArgumentException("A failure count cannot be negative.", nameof(consecutiveFailures));

            if (session.ActiveBranch.HasConfigurationInProgress)
            {
                return new CheckpointCaptureResult(
                    false,
                    null,
                    CheckpointCaptureRejection.BranchConfigurationInProgress
                );
            }

            SessionSnapshot snapshot = session.Snapshot();
            CheckpointState state = new(
                snapshot.CatalogRevision,
                DeterminismService.RevisionIdentity,
                snapshot.SystemIdentity,
                snapshot.SessionIdentity,
                snapshot.SessionSeedIdentity,
                process,
                route,
                consecutiveFailures,
                snapshot.LastAllocatedInstance,
                snapshot.Ordinals,
                Repository(snapshot.Repository),
                Branch(snapshot.ConfirmedBranch),
                Economy(snapshot.Economy)
            );

            return new CheckpointCaptureResult(true, state, CheckpointCaptureRejection.None);
        }

        /// <summary>
        /// Flattens the Repository entries.
        /// </summary>
        /// <param name="entries">The entries in acquisition order.</param>
        /// <returns>The checkpoint entries.</returns>
        private static IReadOnlyList<CheckpointRepositoryItem> Repository(IReadOnlyList<RepositoryEntry> entries)
        {
            List<CheckpointRepositoryItem> repository = new(entries.Count);
            for (int index = 0; index < entries.Count; index++)
            {
                RepositoryEntry entry = entries[index];
                repository.Add(new CheckpointRepositoryItem(
                    entry.Item.InstanceID,
                    entry.Item.DefinitionID,
                    entry.Origin,
                    entry.IsStarterProtected,
                    entry.Suffix,
                    Attachments(entry.Item)
                ));
            }

            return repository;
        }

        /// <summary>
        /// Flattens one item's Patch attachments. Only Instructions have sockets, so every other item
        /// kind carries none rather than an absent list.
        /// </summary>
        /// <param name="item">The Repository item.</param>
        /// <returns>The attachments in socket order.</returns>
        private static IReadOnlyList<CheckpointAttachment> Attachments(RepositoryItem item)
        {
            if (item.Instruction == null)
                return Array.Empty<CheckpointAttachment>();

            IReadOnlyList<PatchAttachment> attached = item.Instruction.AttachedPatches;
            List<CheckpointAttachment> attachments = new(attached.Count);
            for (int index = 0; index < attached.Count; index++)
            {
                PatchAttachment attachment = attached[index];
                attachments.Add(new CheckpointAttachment(
                    attachment.Socket,
                    attachment.Patch.InstanceID,
                    attachment.Patch.Definition.ID.Value
                ));
            }

            return attachments;
        }

        /// <summary>
        /// Flattens the confirmed Branch, or null when none has been confirmed.
        /// </summary>
        /// <param name="confirmed">The confirmed configuration, or null.</param>
        /// <returns>The eligible instances, or null.</returns>
        private static IReadOnlyList<InstanceID> Branch(ActiveBranchConfiguration confirmed)
        {
            if (confirmed == null)
                return null;

            return new List<InstanceID>(confirmed.Eligible);
        }

        /// <summary>
        /// Flattens the economy.
        /// </summary>
        /// <param name="economy">The economy snapshot.</param>
        /// <returns>The checkpoint economy.</returns>
        private static CheckpointEconomy Economy(EconomySnapshot economy)
        {
            List<CheckpointDependency> dependencies = new(economy.Installed.Count);
            for (int index = 0; index < economy.Installed.Count; index++)
            {
                InstalledDependency installed = economy.Installed[index];
                dependencies.Add(new CheckpointDependency(
                    installed.Instance.InstanceID,
                    installed.Instance.Definition.ID.Value,
                    installed.PricePaid,
                    installed.Origin
                ));
            }

            List<CheckpointUtility> utilities = new(economy.Utilities.Count);
            for (int index = 0; index < economy.Utilities.Count; index++)
            {
                UtilityCommitment commitment = economy.Utilities[index];
                utilities.Add(new CheckpointUtility(
                    commitment.Identity,
                    commitment.Definition.ID.Value,
                    commitment.TargetProcess,
                    commitment.OfferID
                ));
            }

            List<CheckpointGrant> grants = new(economy.Grants.Count);
            for (int index = 0; index < economy.Grants.Count; index++)
            {
                PatchGrant grant = economy.Grants[index];
                grants.Add(new CheckpointGrant(
                    grant.GrantIdentity,
                    grant.Definition.ID.Value,
                    grant.Cached,
                    grant.Basis
                ));
            }

            return new CheckpointEconomy(
                economy.Tokens.Value,
                economy.RAMCapacity,
                new CheckpointStarter(economy.Starter.InstanceID, economy.Starter.Definition.ID.Value),
                dependencies,
                utilities,
                grants,
                economy.TransactionCounters
            );
        }
    }
}