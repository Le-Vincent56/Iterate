using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the Active Branch: a confirmed eligible set that survives between Processes, and a draft
    /// that exists only between beginning a configuration and confirming or discarding it. The first
    /// configuration opens over capacity on purpose; later ones restore the prior Branch exactly and
    /// never auto-correct it.
    /// </summary>
    public sealed class ActiveBranchTests
    {
        [Test]
        public void BeginConfiguration_First_SeedsEveryEligibleEntry()
        {
            Repository repository = SeededRepository(3);
            ActiveBranch branch = new();

            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            Assert.IsTrue(branch.HasConfigurationInProgress);
            Assert.AreEqual(3, branch.DraftCount);
        }

        [Test]
        public void BeginConfiguration_First_OpensAtTenOfNineWithTheConditionRequired()
        {
            Repository repository = SeededRepository(9);
            AcquisitionResult condition = repository.Acquire(
                ProgressionFixtures.Condition(ProgressionFixtures.ConditionValueEven),
                AcquisitionOrigin.Reward
            );
            ActiveBranch branch = new();

            branch.BeginConfiguration(
                Constraints(9, required: new[] { ProgressionFixtures.ConditionValueEven }),
                repository.Snapshot()
            );

            Assert.AreEqual(10, branch.DraftCount, "the onboarding Branch opens over capacity on purpose.");
            Assert.IsTrue(branch.StateOf(condition.Entry.Item.InstanceID).HasFlag(BranchInstanceState.Required));
            Assert.IsFalse(branch.Validate().IsValid);
        }

        [Test]
        public void BeginConfiguration_AutoIncludesRequiredContentNotYetHeldInTheDraft()
        {
            Repository repository = SeededRepository(1);
            AcquisitionResult condition = repository.Acquire(
                ProgressionFixtures.Condition(ProgressionFixtures.ConditionValueEven),
                AcquisitionOrigin.Reward
            );
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());
            branch.Exclude(condition.Entry.Item.InstanceID);
            branch.Confirm();

            branch.BeginConfiguration(
                Constraints(9, required: new[] { ProgressionFixtures.ConditionValueEven }),
                repository.Snapshot()
            );

            Assert.IsTrue(branch.StateOf(condition.Entry.Item.InstanceID).HasFlag(BranchInstanceState.Selected));
        }

        [Test]
        public void BeginConfiguration_Later_SeedsFromTheConfirmedSet()
        {
            Repository repository = SeededRepository(3);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());
            branch.Exclude(repository.Entries[0].Item.InstanceID);
            branch.Confirm();

            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            Assert.AreEqual(2, branch.DraftCount, "the prior Branch is restored exactly.");
            Assert.IsFalse(branch.StateOf(repository.Entries[0].Item.InstanceID).HasFlag(BranchInstanceState.Selected));
        }

        [Test]
        public void BeginConfiguration_Later_LeavesNewEntriesUnselected()
        {
            Repository repository = SeededRepository(2);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());
            branch.Confirm();

            AcquisitionResult acquired = repository.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusThree),
                AcquisitionOrigin.Purchase
            );
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            Assert.AreEqual(2, branch.DraftCount);
            BranchInstanceState state = branch.StateOf(acquired.Entry.Item.InstanceID);
            Assert.IsFalse(state.HasFlag(BranchInstanceState.Selected));
            Assert.IsTrue(state.HasFlag(BranchInstanceState.Eligible));
        }

        [Test]
        public void BeginConfiguration_DropsConfirmedEntriesDeletedSinceTheLastConfirmation()
        {
            Repository repository = SeededRepository(3);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());
            branch.Confirm();
            InstanceID deleted = repository.Entries[1].Item.InstanceID;
            repository.Delete(deleted);

            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            Assert.AreEqual(2, branch.DraftCount);
            Assert.IsFalse(branch.StateOf(deleted).HasFlag(BranchInstanceState.Selected));
        }

        [Test]
        public void Confirm_ReportsTheDroppedInstancesOnTheNextRecord()
        {
            Repository repository = SeededRepository(3);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());
            branch.Confirm();
            InstanceID deleted = repository.Entries[1].Item.InstanceID;
            repository.Delete(deleted);
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            BranchConfirmationResult result = branch.Confirm();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, result.Record.DroppedInstances.Count);
            Assert.AreEqual(deleted, result.Record.DroppedInstances[0]);
        }

        [Test]
        public void BeginConfiguration_DoesNotMutateTheConfirmedSetWhenAnEntryIsDeleted()
        {
            Repository repository = SeededRepository(3);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());
            branch.Confirm();
            ActiveBranchConfiguration confirmed = branch.Confirmed;
            repository.Delete(repository.Entries[1].Item.InstanceID);

            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            Assert.AreEqual(3, confirmed.Eligible.Count, "a deletion never rewrites a confirmed Branch.");
            Assert.AreSame(confirmed, branch.Confirmed, "Confirmed changes only at a confirmation.");
        }

        [Test]
        public void Include_AnExcludedEntry_Succeeds()
        {
            Repository repository = SeededRepository(2);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());
            InstanceID id = repository.Entries[0].Item.InstanceID;
            branch.Exclude(id);

            BranchEditResult result = branch.Include(id);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(BranchEditRejection.None, result.Rejection);
            Assert.AreEqual(2, branch.DraftCount);
        }

        [Test]
        public void Include_AnInstanceTheRepositoryDoesNotHold_IsRejected()
        {
            Repository repository = SeededRepository(1);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            BranchEditResult result = branch.Include(new InstanceID(99));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(BranchEditRejection.NotARepositoryItem, result.Rejection);
        }

        [Test]
        public void Include_QuarantinedContent_IsRejected()
        {
            Repository repository = SeededRepository(1);
            AcquisitionResult banned = repository.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusThree),
                AcquisitionOrigin.Purchase
            );
            ActiveBranch branch = new();
            branch.BeginConfiguration(
                Constraints(9, quarantined: new[] { ProgressionFixtures.ValuePlusThree }),
                repository.Snapshot()
            );

            BranchEditResult result = branch.Include(banned.Entry.Item.InstanceID);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(BranchEditRejection.QuarantinedCannotBeIncluded, result.Rejection);
        }

        [Test]
        public void Exclude_RequiredContent_IsRejected()
        {
            Repository repository = SeededRepository(1);
            AcquisitionResult condition = repository.Acquire(
                ProgressionFixtures.Condition(ProgressionFixtures.ConditionValueEven),
                AcquisitionOrigin.Reward
            );
            ActiveBranch branch = new();
            branch.BeginConfiguration(
                Constraints(9, required: new[] { ProgressionFixtures.ConditionValueEven }),
                repository.Snapshot()
            );

            BranchEditResult result = branch.Exclude(condition.Entry.Item.InstanceID);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(BranchEditRejection.RequiredCannotBeExcluded, result.Rejection);
        }

        [Test]
        public void Include_WithNoConfigurationInProgress_IsRejected()
        {
            Repository repository = SeededRepository(1);
            ActiveBranch branch = new();

            BranchEditResult result = branch.Include(repository.Entries[0].Item.InstanceID);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(BranchEditRejection.NoConfigurationInProgress, result.Rejection);
        }

        [Test]
        public void Exclude_WithNoConfigurationInProgress_IsRejected()
        {
            Repository repository = SeededRepository(1);
            ActiveBranch branch = new();

            BranchEditResult result = branch.Exclude(repository.Entries[0].Item.InstanceID);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(BranchEditRejection.NoConfigurationInProgress, result.Rejection);
        }

        [Test]
        public void Validate_OverCapacity_ReportsItWithoutRemovingAnything()
        {
            Repository repository = SeededRepository(4);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(3), repository.Snapshot());

            BranchValidation validation = branch.Validate();

            Assert.IsFalse(validation.IsValid);
            Assert.IsTrue(validation.IsOverCapacity);
            Assert.AreEqual(4, validation.SelectedCount);
            Assert.AreEqual(3, validation.Capacity);
            Assert.AreEqual(4, branch.DraftCount, "nothing is removed automatically.");
        }

        [Test]
        public void Validate_UnderCapacity_IsValid()
        {
            Repository repository = SeededRepository(2);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            BranchValidation validation = branch.Validate();

            Assert.IsTrue(validation.IsValid, "under capacity is legal.");
        }

        [Test]
        public void Validate_MissingRequiredContent_ReportsIt()
        {
            Repository repository = SeededRepository(1);
            ActiveBranch branch = new();

            branch.BeginConfiguration(
                Constraints(9, required: new[] { ProgressionFixtures.ConditionValueEven }),
                repository.Snapshot()
            );
            BranchValidation validation = branch.Validate();

            Assert.IsFalse(validation.IsValid);
            Assert.AreEqual(1, validation.MissingRequired.Count);
            Assert.AreEqual(ProgressionFixtures.ConditionValueEven, validation.MissingRequired[0]);
        }

        [Test]
        public void Validate_QuarantinedContentRestoredFromAPriorBranch_ReportsItPresent()
        {
            Repository repository = SeededRepository(1);
            AcquisitionResult banned = repository.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusThree),
                AcquisitionOrigin.Purchase
            );
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());
            branch.Confirm();

            branch.BeginConfiguration(
                Constraints(9, quarantined: new[] { ProgressionFixtures.ValuePlusThree }),
                repository.Snapshot()
            );
            BranchValidation validation = branch.Validate();

            Assert.IsFalse(validation.IsValid);
            Assert.AreEqual(1, validation.QuarantinedPresent.Count);
            Assert.AreEqual(banned.Entry.Item.InstanceID, validation.QuarantinedPresent[0]);
            Assert.IsTrue(branch.Exclude(banned.Entry.Item.InstanceID).Succeeded, "the player can resolve it.");
        }

        [Test]
        public void BeginConfiguration_First_DoesNotSeedQuarantinedContent()
        {
            Repository repository = SeededRepository(1);
            AcquisitionResult banned = repository.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusThree),
                AcquisitionOrigin.Purchase
            );
            ActiveBranch branch = new();

            branch.BeginConfiguration(
                Constraints(9, quarantined: new[] { ProgressionFixtures.ValuePlusThree }),
                repository.Snapshot()
            );

            Assert.IsFalse(branch.StateOf(banned.Entry.Item.InstanceID).HasFlag(BranchInstanceState.Selected));
            Assert.IsTrue(branch.Validate().IsValid);
        }

        [Test]
        public void Confirm_AnInvalidDraft_IsRejectedAndLeavesConfirmedUntouched()
        {
            Repository repository = SeededRepository(4);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());
            branch.Confirm();
            ActiveBranchConfiguration first = branch.Confirmed;

            branch.BeginConfiguration(Constraints(2), repository.Snapshot());
            BranchConfirmationResult result = branch.Confirm();

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Validation.IsOverCapacity);
            Assert.AreSame(first, branch.Confirmed);
            Assert.IsTrue(branch.HasConfigurationInProgress, "a rejected confirmation keeps the draft open.");
        }

        [Test]
        public void Confirm_AValidDraft_LocksTheEligibleSetAndClosesTheDraft()
        {
            Repository repository = SeededRepository(2);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            BranchConfirmationResult result = branch.Confirm();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(2, branch.Confirmed.Eligible.Count);
            Assert.IsFalse(branch.HasConfigurationInProgress);
            Assert.AreEqual(1, branch.Records.Count);
        }

        [Test]
        public void Confirm_WithNoConfigurationInProgress_IsRejected()
        {
            ActiveBranch branch = new();

            BranchConfirmationResult result = branch.Confirm();

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(branch.Confirmed);
        }

        [Test]
        public void Discard_DropsTheDraftAndLeavesConfirmedUntouched()
        {
            Repository repository = SeededRepository(3);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());
            branch.Confirm();
            ActiveBranchConfiguration confirmed = branch.Confirmed;

            branch.BeginConfiguration(Constraints(9), repository.Snapshot());
            branch.Exclude(repository.Entries[0].Item.InstanceID);
            branch.Discard();

            Assert.IsFalse(branch.HasConfigurationInProgress);
            Assert.AreSame(confirmed, branch.Confirmed);
            Assert.AreEqual(3, branch.Confirmed.Eligible.Count);
        }

        [Test]
        public void StateOf_DerivesRecommendedAndCautionFromTags()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            AcquisitionResult instruction = repository.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                AcquisitionOrigin.Purchase
            );
            AcquisitionResult structure = repository.Acquire(
                ProgressionFixtures.Structure(ProgressionFixtures.RepeatTwo),
                AcquisitionOrigin.Purchase
            );
            ActiveBranch branch = new();
            branch.BeginConfiguration(
                Constraints(9, recommendedTags: new[] { "Add" }, cautionTags: new[] { "Repeat" }),
                repository.Snapshot()
            );

            Assert.IsTrue(branch.StateOf(instruction.Entry.Item.InstanceID).HasFlag(BranchInstanceState.Recommended));
            Assert.IsTrue(branch.StateOf(structure.Entry.Item.InstanceID).HasFlag(BranchInstanceState.Caution));
            Assert.IsFalse(branch.StateOf(instruction.Entry.Item.InstanceID).HasFlag(BranchInstanceState.Caution));
        }

        [Test]
        public void StateOf_AnInstanceTheRepositoryDoesNotHold_IsNone()
        {
            Repository repository = SeededRepository(1);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            Assert.AreEqual(BranchInstanceState.None, branch.StateOf(new InstanceID(99)));
        }

        [Test]
        public void Snapshot_DoesNotChangeWhenTheDraftDoes()
        {
            Repository repository = SeededRepository(3);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            ActiveBranchSnapshot snapshot = branch.Snapshot();
            branch.Exclude(repository.Entries[0].Item.InstanceID);

            Assert.AreEqual(3, snapshot.Draft.Count);
            Assert.AreEqual(2, branch.DraftCount);
        }

        [Test]
        public void Snapshot_BeforeAnyConfirmation_CarriesNoConfirmedSet()
        {
            ActiveBranch branch = new();

            ActiveBranchSnapshot snapshot = branch.Snapshot();

            Assert.IsNull(snapshot.Confirmed);
            Assert.IsFalse(snapshot.HasConfigurationInProgress);
            Assert.AreEqual(0, snapshot.Draft.Count);
        }

        [Test]
        public void BeginConfiguration_WhileOneIsAlreadyInProgress_Throws()
        {
            Repository repository = SeededRepository(1);
            ActiveBranch branch = new();
            branch.BeginConfiguration(Constraints(9), repository.Snapshot());

            Assert.Throws<InvalidOperationException>(
                () => branch.BeginConfiguration(Constraints(9), repository.Snapshot()));
        }

        /// <summary>
        /// Builds a Repository holding the given number of distinct Instruction instances.
        /// </summary>
        /// <param name="count">How many entries to seed.</param>
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
        /// <param name="recommendedTags">The tags marking recommended content.</param>
        /// <param name="cautionTags">The tags marking caution content.</param>
        /// <returns>The constraints.</returns>
        private static BranchConstraints Constraints(
            int capacity,
            IReadOnlyList<string> required = null,
            IReadOnlyList<string> quarantined = null,
            IReadOnlyList<string> recommendedTags = null,
            IReadOnlyList<string> cautionTags = null
        )
        {
            return new BranchConstraints(
                capacity,
                required ?? Array.Empty<string>(),
                quarantined ?? Array.Empty<string>(),
                recommendedTags ?? Array.Empty<string>(),
                cautionTags ?? Array.Empty<string>()
            );
        }
    }
}
