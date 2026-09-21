using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests <see cref="SessionState.Snapshot"/> and <see cref="SessionState.Restore"/>: a Session taken
    /// to a snapshot and rebuilt from it holds the same identities, Repository, Branch and economy, and
    /// every rejection is reached by one mutated snapshot. Restore validates everything before it builds
    /// anything, so a rejected snapshot returns no Session at all rather than a half-built one — there is
    /// no partially restored state for a caller to mistake for a whole one.
    /// </summary>
    public sealed class SessionSnapshotTests
    {
        [Test]
        public void Snapshot_ReportsTheSessionIdentitiesAndCatalogRevision()
        {
            SessionState session = ProgressionFixtures.Session();

            SessionSnapshot snapshot = session.Snapshot();

            Assert.AreEqual("WB-SYS-001", snapshot.SystemIdentity);
            Assert.AreEqual("session-1", snapshot.SessionIdentity);
            Assert.AreEqual("seed-1", snapshot.SessionSeedIdentity);
            Assert.AreEqual("fixture-0.0.1", snapshot.CatalogRevision);
        }

        [Test]
        public void Snapshot_ReportsTheAllocationCounterAndRepository()
        {
            SessionState session = ProgressionFixtures.Session();

            SessionSnapshot snapshot = session.Snapshot();

            Assert.AreEqual(4, snapshot.LastAllocatedInstance, "three starter items then the starter Dependency.");
            Assert.AreEqual(3, snapshot.Repository.Count);
            Assert.AreEqual(0, snapshot.Ordinals.Count);
            Assert.IsNull(snapshot.ConfirmedBranch, "a Session begins with no confirmed Branch.");
        }

        [Test]
        public void Snapshot_ReportsTheEconomy()
        {
            SessionState session = ProgressionFixtures.Session();

            EconomySnapshot economy = session.Snapshot().Economy;

            Assert.AreEqual(0, economy.Tokens.Value);
            Assert.AreEqual(ProgressionFixtures.Parameters().StartingRAM, economy.RAMCapacity);
            Assert.AreEqual(session.StarterDependency, economy.Starter);
            Assert.AreEqual(0, economy.Installed.Count);
            Assert.AreEqual(0, economy.Utilities.Count);
            Assert.AreEqual(0, economy.Grants.Count);
            Assert.AreEqual(0, economy.TransactionCounters.Count);
        }

        [Test]
        public void Restore_OfAnUntouchedSession_YieldsAnEqualSnapshot()
        {
            SessionState session = ProgressionFixtures.Session();
            SessionSnapshot snapshot = session.Snapshot();

            SessionRestoreResult result = SessionState.Restore(ProgressionFixtures.Catalog(), snapshot);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SessionRestoreRejection.None, result.Rejection);
            AssertSnapshotsEqual(snapshot, result.Session.Snapshot());
        }

        [Test]
        public void Restore_OfAnEnrichedSession_YieldsAnEqualSnapshot()
        {
            SessionState session = Enriched();
            SessionSnapshot snapshot = session.Snapshot();

            SessionRestoreResult result = SessionState.Restore(ProgressionFixtures.Catalog(), snapshot);

            Assert.IsTrue(result.Succeeded);
            AssertSnapshotsEqual(snapshot, result.Session.Snapshot());
        }

        [Test]
        public void Restore_ContinuesMintingWhereTheSnapshotLeftOff()
        {
            SessionState session = Enriched();
            SessionSnapshot snapshot = session.Snapshot();

            SessionState restored = SessionState.Restore(ProgressionFixtures.Catalog(), snapshot).Session;

            Assert.AreEqual(snapshot.LastAllocatedInstance, restored.InstanceIDs.LastAllocated);
            Assert.AreEqual(session.InstanceIDs.Next(), restored.InstanceIDs.Next());
        }

        [Test]
        public void Restore_ContinuesTransactionIdentitiesAndOrdinals()
        {
            SessionState session = Enriched();
            session.Economy.NextTransactionIdentity(TransactionKind.Purchase);
            session.Ordinals.Next(ShopState.Purpose, "WB-SHOP-004");

            SessionState restored = SessionState.Restore(ProgressionFixtures.Catalog(), session.Snapshot()).Session;

            Assert.AreEqual(
                session.Economy.NextTransactionIdentity(TransactionKind.Purchase),
                restored.Economy.NextTransactionIdentity(TransactionKind.Purchase)
            );
            Assert.AreEqual(
                session.Ordinals.Next(ShopState.Purpose, "WB-SHOP-004"),
                restored.Ordinals.Next(ShopState.Purpose, "WB-SHOP-004")
            );
        }

        [Test]
        public void Restore_LeavesEveryRecordListEmpty()
        {
            SessionState restored = SessionState.Restore(ProgressionFixtures.Catalog(), Enriched().Snapshot()).Session;

            Assert.AreEqual(0, restored.Repository.Records.Count);
            Assert.AreEqual(0, restored.ActiveBranch.Records.Count);
            Assert.AreEqual(0, restored.Economy.Records.Count);
        }

        [Test]
        public void Restore_RebuildsAConfirmedBranch()
        {
            SessionState session = ProgressionFixtures.Session();
            session.ActiveBranch.BeginConfiguration(Constraints(9), session.Repository.Snapshot());
            session.ActiveBranch.Confirm();
            SessionSnapshot snapshot = session.Snapshot();

            SessionState restored = SessionState.Restore(ProgressionFixtures.Catalog(), snapshot).Session;

            Assert.IsNotNull(restored.ActiveBranch.Confirmed);
            Assert.AreEqual(session.ActiveBranch.Confirmed, restored.ActiveBranch.Confirmed);
            Assert.IsFalse(restored.ActiveBranch.HasConfigurationInProgress);
        }

        [Test]
        public void Restore_CatalogRevisionMismatch()
        {
            AssertRejected(
                ProgressionFixtures.Session().Snapshot() with { CatalogRevision = "fixture-9.9.9" },
                SessionRestoreRejection.CatalogRevisionMismatch
            );
        }

        [Test]
        public void Restore_OrdinalCounterInvalid()
        {
            SessionSnapshot snapshot = ProgressionFixtures.Session().Snapshot() with
            {
                Ordinals = new List<OrdinalCounter> { new(ShopState.Purpose, "WB-SHOP-004", 0) }
            };

            AssertRejected(snapshot, SessionRestoreRejection.OrdinalCounterInvalid);
        }

        [Test]
        public void Restore_TransactionCounterInvalid_OnACountBelowOne()
        {
            AssertRejected(
                WithCounters(new TransactionCounter(TransactionKind.Purchase, 0)),
                SessionRestoreRejection.TransactionCounterInvalid
            );
        }

        [Test]
        public void Restore_TransactionCounterInvalid_OnADuplicateKind()
        {
            AssertRejected(
                WithCounters(
                    new TransactionCounter(TransactionKind.Purchase, 1),
                    new TransactionCounter(TransactionKind.Purchase, 2)
                ),
                SessionRestoreRejection.TransactionCounterInvalid
            );
        }

        [Test]
        public void Restore_DuplicateInstance()
        {
            SessionSnapshot snapshot = ProgressionFixtures.Session().Snapshot();
            List<RepositoryEntry> entries = new(snapshot.Repository) { snapshot.Repository[0] };

            AssertRejected(snapshot with { Repository = entries }, SessionRestoreRejection.DuplicateInstance);
        }

        [Test]
        public void Restore_InstanceAboveCounter()
        {
            AssertRejected(
                ProgressionFixtures.Session().Snapshot() with { LastAllocatedInstance = 2 },
                SessionRestoreRejection.InstanceAboveCounter
            );
        }

        [Test]
        public void Restore_SuffixInvalid()
        {
            SessionSnapshot snapshot = ProgressionFixtures.Session().Snapshot();
            List<RepositoryEntry> entries = new(snapshot.Repository);
            entries[0] = entries[0] with { Suffix = 0 };

            AssertRejected(snapshot with { Repository = entries }, SessionRestoreRejection.SuffixInvalid);
        }

        [Test]
        public void Restore_BranchMemberMissing()
        {
            SessionSnapshot snapshot = ProgressionFixtures.Session().Snapshot() with
            {
                ConfirmedBranch = new ActiveBranchConfiguration(new List<InstanceID> { new(3), new(4) })
            };

            AssertRejected(snapshot, SessionRestoreRejection.BranchMemberMissing);
        }

        [Test]
        public void Restore_StarterConsumesRAM()
        {
            SessionSnapshot snapshot = ProgressionFixtures.Session().Snapshot();
            EconomySnapshot economy = snapshot.Economy with
            {
                Starter = new DependencyInstance(
                    snapshot.Economy.Starter.InstanceID,
                    ProgressionFixtures.Dependency(ProgressionFixtures.CleanBuild, 1)
                )
            };

            AssertRejected(snapshot with { Economy = economy }, SessionRestoreRejection.StarterConsumesRAM);
        }

        [Test]
        public void Restore_RAMOverCapacity()
        {
            SessionSnapshot snapshot = ProgressionFixtures.Session().Snapshot();
            EconomySnapshot economy = snapshot.Economy with
            {
                RAMCapacity = 1,
                Installed = new List<InstalledDependency>
                {
                    Installed(5, ProgressionFixtures.CleanBuild, 1),
                    Installed(6, ProgressionFixtures.CleanBuild, 1)
                }
            };

            AssertRejected(
                snapshot with { LastAllocatedInstance = 6, Economy = economy },
                SessionRestoreRejection.RAMOverCapacity
            );
        }

        [Test]
        public void Restore_DuplicateUtilityCommitment()
        {
            SessionSnapshot snapshot = ProgressionFixtures.Session().Snapshot();
            EconomySnapshot economy = snapshot.Economy with
            {
                Utilities = new List<UtilityCommitment>
                {
                    Commitment(5, "WB-PROC-004"),
                    Commitment(6, "WB-PROC-004")
                }
            };

            AssertRejected(
                snapshot with { LastAllocatedInstance = 6, Economy = economy },
                SessionRestoreRejection.DuplicateUtilityCommitment
            );
        }

        [Test]
        public void Restore_TheSameUtilityOnTwoProcesses_IsNotADuplicate()
        {
            SessionSnapshot snapshot = ProgressionFixtures.Session().Snapshot();
            EconomySnapshot economy = snapshot.Economy with
            {
                Utilities = new List<UtilityCommitment>
                {
                    Commitment(5, "WB-PROC-004"),
                    Commitment(6, "WB-PROC-005")
                }
            };

            SessionRestoreResult result = SessionState.Restore(
                ProgressionFixtures.Catalog(),
                snapshot with { LastAllocatedInstance = 6, Economy = economy }
            );

            Assert.IsTrue(result.Succeeded, "one Utility may be committed to two different Processes.");
        }

        [Test]
        public void Restore_InstanceAboveCounter_IsCheckedOnEveryInstanceBearingPosition()
        {
            // The counter guard is worthless if it only covers the Repository: a grant, a commitment or
            // an installed Dependency above the counter would mint a colliding identity on the next
            // allocation.
            SessionSnapshot snapshot = ProgressionFixtures.Session().Snapshot();
            EconomySnapshot economy = snapshot.Economy with
            {
                Grants = new List<PatchGrant>
                {
                    new(new InstanceID(99), ProgressionFixtures.Patch("WB-PAT-001"), false, "fixture")
                }
            };

            AssertRejected(snapshot with { Economy = economy }, SessionRestoreRejection.InstanceAboveCounter);
        }

        [Test]
        public void Restore_ARejectionBuildsNothing()
        {
            SessionState session = ProgressionFixtures.Session();
            SessionSnapshot snapshot = session.Snapshot();

            SessionRestoreResult result = SessionState.Restore(
                ProgressionFixtures.Catalog(),
                snapshot with { CatalogRevision = "fixture-9.9.9" }
            );

            Assert.IsNull(result.Session);
            Assert.IsNotNull(result.Detail, "a rejection says which snapshot member was wrong.");
            AssertSnapshotsEqual(snapshot, session.Snapshot(), "the source Session is untouched by a rejected restore.");
        }

        /// <summary>
        /// Asserts that restoring the snapshot fails with the expected rejection and builds no Session.
        /// </summary>
        /// <param name="snapshot">The mutated snapshot.</param>
        /// <param name="expected">The rejection the mutation must produce.</param>
        private static void AssertRejected(SessionSnapshot snapshot, SessionRestoreRejection expected)
        {
            SessionRestoreResult result = SessionState.Restore(ProgressionFixtures.Catalog(), snapshot);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(expected, result.Rejection);
            Assert.IsNull(result.Session);
        }

        /// <summary>
        /// Asserts two snapshots hold the same values, comparing every list element-wise.
        /// </summary>
        /// <param name="expected">The expected snapshot.</param>
        /// <param name="actual">The snapshot under test.</param>
        /// <param name="because">Why the two must agree.</param>
        private static void AssertSnapshotsEqual(
            SessionSnapshot expected,
            SessionSnapshot actual,
            string because = "a restored Session holds what its snapshot described."
        )
        {
            Assert.AreEqual(expected.SystemIdentity, actual.SystemIdentity, because);
            Assert.AreEqual(expected.SessionIdentity, actual.SessionIdentity, because);
            Assert.AreEqual(expected.SessionSeedIdentity, actual.SessionSeedIdentity, because);
            Assert.AreEqual(expected.CatalogRevision, actual.CatalogRevision, because);
            Assert.AreEqual(expected.LastAllocatedInstance, actual.LastAllocatedInstance, because);
            // Element-wise, not Assert.AreEqual: ActiveBranchConfiguration is a record over a list, so
            // its generated equality is reference equality on that list. Comparing the records would
            // pass here for the wrong reason — SessionState.Restore stores the very same configuration
            // object — and would go on passing even if the instances inside it were wrong.
            if (expected.ConfirmedBranch == null)
            {
                Assert.IsNull(actual.ConfirmedBranch, because);
            }
            else
            {
                Assert.IsNotNull(actual.ConfirmedBranch, because);
                CollectionAssert.AreEqual(expected.ConfirmedBranch.Eligible, actual.ConfirmedBranch.Eligible, because);
            }

            CollectionAssert.AreEqual(expected.Ordinals, actual.Ordinals, because);
            CollectionAssert.AreEqual(expected.Repository, actual.Repository, because);
            Assert.AreEqual(expected.Economy.Tokens, actual.Economy.Tokens, because);
            Assert.AreEqual(expected.Economy.RAMCapacity, actual.Economy.RAMCapacity, because);
            Assert.AreEqual(expected.Economy.Starter, actual.Economy.Starter, because);
            CollectionAssert.AreEqual(expected.Economy.Installed, actual.Economy.Installed, because);
            CollectionAssert.AreEqual(expected.Economy.Utilities, actual.Economy.Utilities, because);
            CollectionAssert.AreEqual(expected.Economy.Grants, actual.Economy.Grants, because);
            CollectionAssert.AreEqual(expected.Economy.TransactionCounters, actual.Economy.TransactionCounters, because);
        }

        /// <summary>
        /// Builds a Session carrying two further acquisitions, an installed Dependency, a Utility
        /// commitment, a Patch grant and a Token balance, so a snapshot has something to lose.
        /// </summary>
        /// <returns>The enriched Session.</returns>
        private static SessionState Enriched()
        {
            SessionState session = ProgressionFixtures.Session();
            session.Repository.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusThree),
                AcquisitionOrigin.Purchase
            );
            session.Repository.Acquire(
                ProgressionFixtures.Structure(ProgressionFixtures.RepeatTwo),
                AcquisitionOrigin.Reward
            );
            session.Economy.Tokens.Credit(new TokenAmount(7), TokenBasis.RewardTokens, "fixture");
            session.Economy.Dependencies.Install(
                ProgressionFixtures.Dependency(ProgressionFixtures.CleanBuild, 1),
                session.InstanceIDs.Next(),
                3,
                DependencyOrigin.Purchase
            );
            session.Economy.CommitUtility(
                EconomyFixtures.Utility(EconomyFixtures.StartingBytesUtility, "STARTING_BYTES", 1),
                EconomyFixtures.Process("WB-PROC-004"),
                "OFF-001"
            );
            session.Economy.Grant(ProgressionFixtures.Patch("WB-PAT-001"), true, "fixture");
            return session;
        }

        /// <summary>
        /// Builds an installed Dependency entry at the given instance value.
        /// </summary>
        /// <param name="instance">The instance identity's value.</param>
        /// <param name="definitionID">The Dependency definition's identity.</param>
        /// <param name="ram">The RAM the Dependency consumes.</param>
        /// <returns>The installed entry.</returns>
        private static InstalledDependency Installed(int instance, string definitionID, int ram)
        {
            return new InstalledDependency(
                new DependencyInstance(new InstanceID(instance), ProgressionFixtures.Dependency(definitionID, ram)),
                3,
                DependencyOrigin.Purchase
            );
        }

        /// <summary>
        /// Builds a Utility commitment of the one fixture Utility against a Process.
        /// </summary>
        /// <param name="instance">The instance identity's value.</param>
        /// <param name="process">The target Process's identity.</param>
        /// <returns>The commitment.</returns>
        private static UtilityCommitment Commitment(int instance, string process)
        {
            return new UtilityCommitment(
                new InstanceID(instance),
                EconomyFixtures.Utility(EconomyFixtures.StartingBytesUtility, "STARTING_BYTES", 1),
                EconomyFixtures.Process(process),
                "OFF-001"
            );
        }

        /// <summary>
        /// Takes a fresh Session's snapshot with its transaction counters replaced.
        /// </summary>
        /// <param name="counters">The counters to write into the snapshot.</param>
        /// <returns>The mutated snapshot.</returns>
        private static SessionSnapshot WithCounters(params TransactionCounter[] counters)
        {
            SessionSnapshot snapshot = ProgressionFixtures.Session().Snapshot();
            return snapshot with { Economy = snapshot.Economy with { TransactionCounters = counters } };
        }

        /// <summary>
        /// Builds Branch constraints with the given capacity and no content rules.
        /// </summary>
        /// <param name="capacity">The Branch capacity.</param>
        /// <returns>The constraints.</returns>
        private static BranchConstraints Constraints(int capacity)
        {
            return new BranchConstraints(
                capacity,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()
            );
        }
    }
}
