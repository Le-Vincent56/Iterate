using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests <see cref="Repository.Restore"/>: a Repository rebuilt from entries answers every lookup the
    /// acquired one does and resumes suffix allocation above the highest suffix it was given, so the
    /// number the player learned for an item survives a save. Records are not restored — they describe
    /// what happened this sitting, not what the Session holds. Restore takes a validated input, so its
    /// contract violations throw rather than reject.
    /// </summary>
    public sealed class RepositoryRestoreTests
    {
        [Test]
        public void Restore_RebuildsEntriesInTheGivenOrder()
        {
            IReadOnlyList<RepositoryEntry> entries = TwoInstructions();

            Repository restored = Repository.Restore(new InstanceIDSource(), entries);

            Assert.AreEqual(2, restored.Entries.Count);
            Assert.AreEqual(new InstanceID(1), restored.Entries[0].Item.InstanceID);
            Assert.AreEqual(new InstanceID(2), restored.Entries[1].Item.InstanceID);
        }

        [Test]
        public void Restore_AnswersInstanceLookups()
        {
            Repository restored = Repository.Restore(new InstanceIDSource(), TwoInstructions());

            Assert.IsTrue(restored.Contains(new InstanceID(2)));
            Assert.IsTrue(restored.TryGet(new InstanceID(2), out RepositoryEntry entry));
            Assert.AreEqual(2, entry.Suffix);
            Assert.IsFalse(restored.Contains(new InstanceID(99)));
        }

        [Test]
        public void Restore_AnswersDefinitionQueries()
        {
            Repository restored = Repository.Restore(new InstanceIDSource(), TwoInstructions());

            Assert.AreEqual(2, restored.EntriesOf(ProgressionFixtures.ValuePlusTwo).Count);
            Assert.AreEqual(0, restored.EntriesOf(ProgressionFixtures.ValuePlusThree).Count);
            Assert.IsTrue(restored.TryResolveContent(
                ProgressionFixtures.ValuePlusTwo,
                Array.Empty<InstanceID>(),
                out RepositoryEntry resolved
            ));
            Assert.AreEqual(1, resolved.Suffix, "resolution still takes the lowest-suffix copy.");
        }

        [Test]
        public void Restore_ResumesSuffixAllocationAboveTheHighestRestoredSuffix()
        {
            Repository restored = Repository.Restore(InstanceIDSource.Restore(2), TwoInstructions());

            AcquisitionResult acquired = restored.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                AcquisitionOrigin.Purchase
            );

            Assert.IsTrue(acquired.Succeeded);
            Assert.AreEqual(3, acquired.Entry.Suffix, "a restored definition never reissues a suffix.");
            Assert.AreEqual(new InstanceID(3), acquired.Entry.Item.InstanceID);
        }

        [Test]
        public void Restore_TracksSuffixesPerDefinitionNotAcrossThem()
        {
            List<RepositoryEntry> entries = new(TwoInstructions())
            {
                Entry(ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusThree), 3, 1)
            };

            Repository restored = Repository.Restore(InstanceIDSource.Restore(3), entries);
            AcquisitionResult acquired = restored.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusThree),
                AcquisitionOrigin.Reward
            );

            Assert.AreEqual(2, acquired.Entry.Suffix);
        }

        [Test]
        public void Restore_AnUnseenDefinition_AllocatesFromOne()
        {
            Repository restored = Repository.Restore(InstanceIDSource.Restore(2), TwoInstructions());

            AcquisitionResult acquired = restored.Acquire(
                ProgressionFixtures.Directive(ProgressionFixtures.Overclock),
                AcquisitionOrigin.Reward
            );

            Assert.AreEqual(1, acquired.Entry.Suffix);
        }

        [Test]
        public void Restore_LeavesRecordsEmpty()
        {
            Repository restored = Repository.Restore(new InstanceIDSource(), TwoInstructions());

            Assert.AreEqual(0, restored.Records.Count, "restoring is not a transaction.");
        }

        [Test]
        public void Restore_KeepsOriginAndStarterProtection()
        {
            List<RepositoryEntry> entries = new()
            {
                new RepositoryEntry(
                    RepositoryItem.From(
                        ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                        new InstanceID(1)
                    ),
                    AcquisitionOrigin.Starter,
                    true,
                    1
                )
            };

            Repository restored = Repository.Restore(new InstanceIDSource(), entries);

            Assert.AreEqual(AcquisitionOrigin.Starter, restored.Entries[0].Origin);
            Assert.IsTrue(restored.Entries[0].IsStarterProtected);
            Assert.AreEqual(
                RepositoryRejection.StarterProtected,
                restored.Delete(new InstanceID(1)).Rejection,
                "a restored starter entry is still protected."
            );
        }

        [Test]
        public void Restore_SnapshotEqualsTheEntriesItWasGiven()
        {
            IReadOnlyList<RepositoryEntry> entries = TwoInstructions();

            RepositorySnapshot snapshot = Repository.Restore(new InstanceIDSource(), entries).Snapshot();

            Assert.AreEqual(entries.Count, snapshot.Entries.Count);
            for (int index = 0; index < entries.Count; index++)
            {
                Assert.AreEqual(entries[index], snapshot.Entries[index]);
            }
        }

        [Test]
        public void Restore_RoundTripsAnAcquiredRepository()
        {
            Repository original = ProgressionFixtures.EmptyRepository();
            original.Acquire(ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo), AcquisitionOrigin.Starter);
            original.Acquire(ProgressionFixtures.Structure(ProgressionFixtures.RepeatTwo), AcquisitionOrigin.Reward);

            RepositorySnapshot snapshot = original.Snapshot();
            Repository restored = Repository.Restore(InstanceIDSource.Restore(2), snapshot.Entries);

            Assert.AreEqual(snapshot.Entries.Count, restored.Snapshot().Entries.Count);
            for (int index = 0; index < snapshot.Entries.Count; index++)
            {
                Assert.AreEqual(snapshot.Entries[index], restored.Snapshot().Entries[index]);
            }
        }

        [Test]
        public void Restore_NullIdentitySource_ThrowsTheConstructorsOwnRejection()
        {
            // Restore builds through the ordinary constructor rather than repeating its null rule, so a
            // null identity source fails exactly as `new Repository(null)` does.
            Assert.Throws<ArgumentNullException>(() => Repository.Restore(null, TwoInstructions()));
        }

        [Test]
        public void Restore_NullEntries_Throws()
        {
            Assert.Throws<ArgumentException>(() => Repository.Restore(new InstanceIDSource(), null));
        }

        [Test]
        public void Restore_NullEntry_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => Repository.Restore(new InstanceIDSource(), new RepositoryEntry[] { null })
            );
        }

        [Test]
        public void Restore_DuplicateInstance_Throws()
        {
            List<RepositoryEntry> entries = new()
            {
                Entry(ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo), 1, 1),
                Entry(ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusThree), 1, 1)
            };

            Assert.Throws<ArgumentException>(() => Repository.Restore(new InstanceIDSource(), entries));
        }

        [Test]
        public void Restore_SuffixBelowOne_Throws()
        {
            List<RepositoryEntry> entries = new()
            {
                Entry(ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo), 1, 0)
            };

            Assert.Throws<ArgumentException>(() => Repository.Restore(new InstanceIDSource(), entries));
        }

        /// <summary>
        /// Builds two entries of one Instruction definition carrying instances 1 and 2 and suffixes 1
        /// and 2, which is what acquiring the same definition twice produces.
        /// </summary>
        /// <returns>The two entries in acquisition order.</returns>
        private static IReadOnlyList<RepositoryEntry> TwoInstructions()
        {
            return new List<RepositoryEntry>
            {
                Entry(ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo), 1, 1),
                Entry(ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo), 2, 2)
            };
        }

        /// <summary>
        /// Builds one purchased, unprotected entry.
        /// </summary>
        /// <param name="definition">The content definition the entry holds.</param>
        /// <param name="instance">The instance identity's value.</param>
        /// <param name="suffix">The per-definition acquisition suffix.</param>
        /// <returns>The entry.</returns>
        private static RepositoryEntry Entry(ContentDefinition definition, int instance, int suffix)
        {
            return new RepositoryEntry(
                RepositoryItem.From(definition, new InstanceID(instance)),
                AcquisitionOrigin.Purchase,
                false,
                suffix
            );
        }
    }
}
