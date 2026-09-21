using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests <see cref="ActiveBranch.Restore"/>: a Branch rebuilt from a confirmed configuration opens
    /// the next configuration from that set exactly as a between-Processes Branch does, and a Branch
    /// restored from no configuration seeds the first one from the Repository. Nothing is in progress
    /// after a restore, because a draft exists only between beginning a configuration and ending it and
    /// a checkpoint is never taken mid-draft.
    /// </summary>
    public sealed class ActiveBranchRestoreTests
    {
        [Test]
        public void Restore_SetsTheConfirmedSet()
        {
            ActiveBranchConfiguration confirmed = Configuration(1, 2);

            ActiveBranch branch = ActiveBranch.Restore(confirmed);

            Assert.AreEqual(confirmed, branch.Confirmed);
        }

        [Test]
        public void Restore_LeavesNoConfigurationInProgress()
        {
            ActiveBranch branch = ActiveBranch.Restore(Configuration(1, 2));

            Assert.IsFalse(branch.HasConfigurationInProgress);
            Assert.AreEqual(0, branch.DraftCount);
        }

        [Test]
        public void Restore_LeavesRecordsEmpty()
        {
            ActiveBranch branch = ActiveBranch.Restore(Configuration(1, 2));

            Assert.AreEqual(0, branch.Records.Count, "restoring is not a confirmation.");
        }

        [Test]
        public void Restore_StateOf_IsNoneBecauseNoConfigurationIsOpen()
        {
            // StateOf describes an instance within the *open* configuration and short-circuits when none
            // is open, so a restored Branch reports None for a confirmed member as for anything else.
            ActiveBranch branch = ActiveBranch.Restore(Configuration(1, 2));

            Assert.AreEqual(BranchInstanceState.None, branch.StateOf(new InstanceID(1)));
        }

        [Test]
        public void Restore_Snapshot_ReportsTheConfirmedSetAndAnEmptyDraft()
        {
            ActiveBranchConfiguration confirmed = Configuration(1, 2);

            ActiveBranchSnapshot snapshot = ActiveBranch.Restore(confirmed).Snapshot();

            Assert.AreEqual(confirmed, snapshot.Confirmed);
            Assert.AreEqual(0, snapshot.Draft.Count);
            Assert.IsFalse(snapshot.HasConfigurationInProgress);
        }

        [Test]
        public void Restore_ThenBeginConfiguration_SeedsFromTheConfirmedSet()
        {
            Repository repository = SeededRepository(3);
            IReadOnlyList<RepositoryEntry> entries = repository.Snapshot().Entries;
            ActiveBranch branch = ActiveBranch.Restore(
                new ActiveBranchConfiguration(new List<InstanceID> { entries[0].Item.InstanceID })
            );

            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            Assert.IsTrue(branch.HasConfigurationInProgress);
            Assert.AreEqual(1, branch.DraftCount, "a later configuration restores the prior Branch, not everything held.");
            Assert.IsTrue(branch.StateOf(entries[0].Item.InstanceID).HasFlag(BranchInstanceState.Selected));
            Assert.IsFalse(branch.StateOf(entries[1].Item.InstanceID).HasFlag(BranchInstanceState.Selected));
        }

        [Test]
        public void Restore_ThenConfirm_ReportsLostMembersOfTheRestoredSet()
        {
            Repository repository = SeededRepository(2);
            IReadOnlyList<RepositoryEntry> entries = repository.Snapshot().Entries;
            ActiveBranch branch = ActiveBranch.Restore(new ActiveBranchConfiguration(new List<InstanceID>
            {
                entries[0].Item.InstanceID,
                new(99)
            }));

            branch.BeginConfiguration(Constraints(9), repository.Snapshot());
            BranchConfirmationResult result = branch.Confirm();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, result.Record.DroppedInstances.Count, "an instance the Repository has lost is reported, not silently kept.");
            Assert.AreEqual(new InstanceID(99), result.Record.DroppedInstances[0]);
        }

        [Test]
        public void Restore_Null_ThenBeginConfiguration_SeedsFromTheRepositoryAsAFirstConfigurationDoes()
        {
            Repository repository = SeededRepository(3);
            ActiveBranch restored = ActiveBranch.Restore(null);
            ActiveBranch fresh = new();

            restored.BeginConfiguration(Constraints(9), repository.Snapshot());
            fresh.BeginConfiguration(Constraints(9), repository.Snapshot());

            Assert.IsNull(restored.Confirmed);
            Assert.AreEqual(fresh.DraftCount, restored.DraftCount);
            Assert.AreEqual(3, restored.DraftCount);
        }

        [Test]
        public void Restore_ThenBeginConfiguration_HonoursQuarantineAndRequiredTheSameWay()
        {
            Repository repository = SeededRepository(1);
            AcquisitionResult condition = repository.Acquire(
                ProgressionFixtures.Condition(ProgressionFixtures.ConditionValueEven),
                AcquisitionOrigin.Reward
            );
            ActiveBranch branch = ActiveBranch.Restore(new ActiveBranchConfiguration(new List<InstanceID>()));

            branch.BeginConfiguration(
                Constraints(9, required: new[] { ProgressionFixtures.ConditionValueEven }),
                repository.Snapshot()
            );

            Assert.IsTrue(branch.StateOf(condition.Entry.Item.InstanceID).HasFlag(BranchInstanceState.Required));
            Assert.IsTrue(branch.StateOf(condition.Entry.Item.InstanceID).HasFlag(BranchInstanceState.Selected));
        }

        [Test]
        public void Restore_RoundTripsAConfirmedBranch()
        {
            Repository repository = SeededRepository(3);
            ActiveBranch original = new();
            original.BeginConfiguration(Constraints(9), repository.Snapshot());
            original.Exclude(repository.Snapshot().Entries[0].Item.InstanceID);
            original.Confirm();

            ActiveBranch restored = ActiveBranch.Restore(original.Confirmed);

            Assert.AreEqual(original.Confirmed, restored.Confirmed);
            Assert.AreEqual(original.HasConfigurationInProgress, restored.HasConfigurationInProgress);
            Assert.AreEqual(original.DraftCount, restored.DraftCount);
        }

        /// <summary>
        /// Builds a confirmed configuration over the given instance values.
        /// </summary>
        /// <param name="instances">The eligible instance values.</param>
        /// <returns>The configuration.</returns>
        private static ActiveBranchConfiguration Configuration(params int[] instances)
        {
            List<InstanceID> eligible = new(instances.Length);
            for (int index = 0; index < instances.Length; index++)
            {
                eligible.Add(new InstanceID(instances[index]));
            }

            return new ActiveBranchConfiguration(eligible);
        }

        /// <summary>
        /// Builds a Repository holding the given number of purchased Instructions.
        /// </summary>
        /// <param name="count">How many entries to acquire.</param>
        /// <returns>The seeded Repository.</returns>
        private static Repository SeededRepository(int count)
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            for (int index = 0; index < count; index++)
            {
                repository.Acquire(
                    ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                    AcquisitionOrigin.Purchase
                );
            }

            return repository;
        }

        /// <summary>
        /// Builds Branch constraints, defaulting every list to empty.
        /// </summary>
        /// <param name="capacity">The Branch capacity.</param>
        /// <param name="required">The Required content IDs.</param>
        /// <param name="quarantined">The Quarantined content IDs.</param>
        /// <returns>The constraints.</returns>
        private static BranchConstraints Constraints(
            int capacity,
            IReadOnlyList<string> required = null,
            IReadOnlyList<string> quarantined = null
        )
        {
            return new BranchConstraints(
                capacity,
                required ?? Array.Empty<string>(),
                quarantined ?? Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()
            );
        }
    }
}
