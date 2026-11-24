using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Persistence;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Persistence.Tests
{
    /// <summary>
    /// Tests <see cref="CheckpointCapture"/>: a Session flattened into pure data that names content by
    /// identity rather than holding it. Capture is refused outright while a Branch configuration is open,
    /// because a draft is a half-made decision and a checkpoint that preserved one would restore the
    /// player into an edit they never finished.
    /// </summary>
    public sealed class CheckpointCaptureTests
    {
        [Test]
        public void Capture_CarriesTheRevisionsAndIdentities()
        {
            CheckpointState state = Captured(Session());

            Assert.AreEqual(PersistenceFixtures.Revision, state.CatalogRevision);
            Assert.AreEqual(DeterminismService.RevisionIdentity, state.RandomServiceRevision);
            Assert.AreEqual("WB-SYS-001", state.SystemIdentity);
            Assert.AreEqual("session-1", state.SessionIdentity);
            Assert.AreEqual("seed-1", state.SessionSeedIdentity);
        }

        [Test]
        public void Capture_CarriesTheProcessAndANullRoute()
        {
            CheckpointState state = Captured(Session());

            Assert.AreEqual(new ProcessID(PersistenceFixtures.Process), state.Process);
            Assert.IsNull(state.Route);
        }

        [Test]
        public void Capture_CarriesARouteWhenOneWasTaken()
        {
            CheckpointCaptureResult result = CheckpointCapture.Capture(
                Session(),
                new ProcessID(PersistenceFixtures.Process),
                new RouteID(PersistenceFixtures.Route),
                0
            );

            Assert.AreEqual(new RouteID(PersistenceFixtures.Route), result.State.Route);
        }

        [Test]
        public void Capture_PassesTheFailureCountThrough()
        {
            CheckpointCaptureResult result = CheckpointCapture.Capture(
                Session(),
                new ProcessID(PersistenceFixtures.Process),
                null,
                2
            );

            Assert.AreEqual(2, result.State.ConsecutiveFailures);
        }

        [Test]
        public void Capture_ANegativeFailureCount_Throws()
        {
            SessionState session = Session();

            Assert.Throws<ArgumentException>(
                () => CheckpointCapture.Capture(session, new ProcessID(PersistenceFixtures.Process), null, -1)
            );
        }

        [Test]
        public void Capture_CarriesTheAllocationCounterAndOrdinals()
        {
            SessionState session = Session();
            session.Ordinals.Next(ShopState.Purpose, "WB-SHOP-004");

            CheckpointState state = Captured(session);

            Assert.AreEqual(session.InstanceIDs.LastAllocated, state.LastAllocatedInstance);
            Assert.AreEqual(1, state.Ordinals.Count);
            Assert.AreEqual(new OrdinalCounter(ShopState.Purpose, "WB-SHOP-004", 1), state.Ordinals[0]);
        }

        [Test]
        public void Capture_FlattensRepositoryEntriesToIdentitiesAndDefinitions()
        {
            SessionState session = Session();
            IReadOnlyList<RepositoryEntry> entries = session.Repository.Entries;

            CheckpointState state = Captured(session);

            Assert.AreEqual(entries.Count, state.Repository.Count);
            for (int index = 0; index < entries.Count; index++)
            {
                Assert.AreEqual(entries[index].Item.InstanceID, state.Repository[index].Instance);
                Assert.AreEqual(entries[index].Item.DefinitionID, state.Repository[index].Definition);
                Assert.AreEqual(entries[index].Origin, state.Repository[index].Origin);
                Assert.AreEqual(entries[index].IsStarterProtected, state.Repository[index].StarterProtected);
                Assert.AreEqual(entries[index].Suffix, state.Repository[index].Suffix);
            }
        }

        [Test]
        public void Capture_KeepsTheAcquisitionSuffixOfASecondCopy()
        {
            // Two copies of one definition carry suffixes 1 and 2, and the suffix is the number the
            // player learned for the item, so it must survive a capture unchanged.
            SessionState session = Session();

            CheckpointState state = Captured(session);

            Assert.AreEqual(1, state.Repository[0].Suffix);
            Assert.AreEqual(PersistenceFixtures.Instruction, state.Repository[1].Definition);
            Assert.AreEqual(2, state.Repository[1].Suffix);
        }

        [Test]
        public void Capture_CarriesAnAttachedPatchWithItsSocketAndInstance()
        {
            SessionState session = Session();
            InstanceID host = session.Repository.Entries[0].Item.InstanceID;
            InstanceID patch = session.InstanceIDs.Next();
            Attach(session, host, 2, patch);

            CheckpointRepositoryItem item = Find(Captured(session), host);

            Assert.AreEqual(1, item.Attachments.Count);
            Assert.AreEqual(2, item.Attachments[0].Socket);
            Assert.AreEqual(patch, item.Attachments[0].Instance);
            Assert.AreEqual(PersistenceFixtures.Patch, item.Attachments[0].Definition);
        }

        [Test]
        public void Capture_AStructureCarriesNoAttachments()
        {
            // Only Instructions have sockets, so the capture reads an attachment list that is not there
            // rather than assuming every item kind has one.
            SessionState session = Session();
            InstanceID structure = session.Repository.Entries[2].Item.InstanceID;

            CheckpointRepositoryItem item = Find(Captured(session), structure);

            Assert.AreEqual(PersistenceFixtures.Structure, item.Definition);
            Assert.AreEqual(0, item.Attachments.Count);
        }

        [Test]
        public void Capture_CarriesANullBranchWhenNoneIsConfirmed()
        {
            Assert.IsNull(Captured(Session()).Branch);
        }

        [Test]
        public void Capture_CarriesAConfirmedBranch()
        {
            SessionState session = Session();
            session.ActiveBranch.BeginConfiguration(Constraints(), session.Repository.Snapshot());
            session.ActiveBranch.Confirm();

            CollectionAssert.AreEqual(session.ActiveBranch.Confirmed.Eligible, Captured(session).Branch);
        }

        [Test]
        public void Capture_CarriesTheEconomy()
        {
            SessionState session = Session();
            session.Economy.Dependencies.Install(
                PersistenceFixtures.DependencyOf(PersistenceFixtures.CostlyDependency, 1),
                session.InstanceIDs.Next(),
                3,
                DependencyOrigin.Purchase
            );
            session.Economy.Grant(PersistenceFixtures.PatchOf(PersistenceFixtures.Patch), true, "reward");
            session.Economy.NextTransactionIdentity(TransactionKind.Purchase);

            CheckpointEconomy economy = Captured(session).Economy;

            Assert.AreEqual(9, economy.Tokens);
            Assert.AreEqual(session.Economy.Dependencies.Capacity, economy.RAMCapacity);
            Assert.AreEqual(session.StarterDependency.InstanceID, economy.Starter.Instance);
            Assert.AreEqual(PersistenceFixtures.StarterDependency, economy.Starter.Definition);
            Assert.AreEqual(1, economy.Dependencies.Count);
            Assert.AreEqual(PersistenceFixtures.CostlyDependency, economy.Dependencies[0].Definition);
            Assert.AreEqual(3, economy.Dependencies[0].PricePaid);
            Assert.AreEqual(DependencyOrigin.Purchase, economy.Dependencies[0].Origin);
            Assert.AreEqual(1, economy.Grants.Count);
            Assert.IsTrue(economy.Grants[0].Cached);
            Assert.AreEqual("reward", economy.Grants[0].Basis);
            Assert.AreEqual(1, economy.TransactionCounters.Count);
            Assert.AreEqual(new TransactionCounter(TransactionKind.Purchase, 1), economy.TransactionCounters[0]);
        }

        [Test]
        public void Capture_CarriesAUtilityCommitment()
        {
            SessionState session = Session();
            session.Economy.CommitUtility(
                PersistenceFixtures.UtilityOf(PersistenceFixtures.Utility),
                new ProcessID(PersistenceFixtures.Process),
                "OFF-901"
            );

            CheckpointEconomy economy = Captured(session).Economy;

            Assert.AreEqual(1, economy.Utilities.Count);
            Assert.AreEqual(PersistenceFixtures.Utility, economy.Utilities[0].Definition);
            Assert.AreEqual(new ProcessID(PersistenceFixtures.Process), economy.Utilities[0].TargetProcess);
            Assert.AreEqual("OFF-901", economy.Utilities[0].OfferID);
        }

        [Test]
        public void Capture_WhileABranchConfigurationIsOpen_IsRejected()
        {
            SessionState session = Session();
            session.ActiveBranch.BeginConfiguration(Constraints(), session.Repository.Snapshot());

            CheckpointCaptureResult result = CheckpointCapture.Capture(
                session,
                new ProcessID(PersistenceFixtures.Process),
                null,
                0
            );

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(CheckpointCaptureRejection.BranchConfigurationInProgress, result.Rejection);
            Assert.IsNull(result.State, "a rejected capture produces no checkpoint at all.");
        }

        [Test]
        public void Capture_AfterAConfigurationIsDiscarded_Succeeds()
        {
            SessionState session = Session();
            session.ActiveBranch.BeginConfiguration(Constraints(), session.Repository.Snapshot());
            session.ActiveBranch.Discard();

            CheckpointCaptureResult result = CheckpointCapture.Capture(
                session,
                new ProcessID(PersistenceFixtures.Process),
                null,
                0
            );

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(CheckpointCaptureRejection.None, result.Rejection);
        }

        /// <summary>
        /// Captures a Session at the fixture Process with no route and no failures.
        /// </summary>
        /// <param name="session">The Session to capture.</param>
        /// <returns>The captured checkpoint.</returns>
        private static CheckpointState Captured(SessionState session)
        {
            CheckpointCaptureResult result = CheckpointCapture.Capture(
                session,
                new ProcessID(PersistenceFixtures.Process),
                null,
                0
            );

            Assert.IsTrue(result.Succeeded, "the fixture Session must capture.");
            return result.State;
        }

        /// <summary>
        /// The captured entry for one instance.
        /// </summary>
        /// <param name="state">The checkpoint to search.</param>
        /// <param name="instance">The instance to find.</param>
        /// <returns>The entry.</returns>
        private static CheckpointRepositoryItem Find(CheckpointState state, InstanceID instance)
        {
            for (int index = 0; index < state.Repository.Count; index++)
            {
                if (state.Repository[index].Instance == instance)
                    return state.Repository[index];
            }

            Assert.Fail($"the checkpoint does not hold {instance}.");
            return null;
        }

        /// <summary>
        /// Attaches a Patch instance to a held Instruction through the Repository's own replacement path.
        /// </summary>
        /// <param name="session">The Session holding the host.</param>
        /// <param name="host">The Instruction instance to attach to.</param>
        /// <param name="socket">The socket to fill.</param>
        /// <param name="patch">The Patch instance's identity.</param>
        private static void Attach(
            SessionState session,
            InstanceID host,
            int socket,
            InstanceID patch
        )
        {
            session.Repository.TryGet(host, out RepositoryEntry entry);
            InstructionInstance derived = entry.Item.Instruction.WithAttachment(new PatchAttachment(
                socket,
                new PatchInstance(patch, PersistenceFixtures.PatchOf(PersistenceFixtures.Patch))
            ));

            session.Repository.Replace(host, entry.Item with { Instruction = derived });
        }

        /// <summary>
        /// A Session over the fixture catalog holding nine Tokens.
        /// </summary>
        /// <returns>The Session.</returns>
        private static SessionState Session()
        {
            return PersistenceFixtures.Session(PersistenceFixtures.Catalog());
        }

        /// <summary>
        /// Branch constraints with room for everything the fixture Repository holds.
        /// </summary>
        /// <returns>The constraints.</returns>
        private static BranchConstraints Constraints()
        {
            return new BranchConstraints(
                9,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()
            );
        }
    }
}
