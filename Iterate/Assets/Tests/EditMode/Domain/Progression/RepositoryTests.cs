using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the Repository of unique instances: the single item factory, acquisition-order entries,
    /// per-definition suffixes that are never reused after a deletion, starter protection, derived
    /// replacement under one identity, and the one content-resolution query every later site calls.
    /// </summary>
    public sealed class RepositoryTests
    {
        [Test]
        public void From_AnInstruction_ProducesAnInstructionItem()
        {
            RepositoryItem item = RepositoryItem.From(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                new InstanceID(1)
            );

            Assert.AreEqual(RepositoryItemKind.Instruction, item.Kind);
            Assert.AreEqual(ProgressionFixtures.ValuePlusTwo, item.DefinitionID);
            Assert.IsNotNull(item.Instruction);
            Assert.IsNull(item.Structure);
            Assert.IsNull(item.Directive);
        }

        [Test]
        public void From_AStructure_ProducesAStructureItem()
        {
            RepositoryItem item = RepositoryItem.From(
                ProgressionFixtures.Structure(ProgressionFixtures.RepeatTwo),
                new InstanceID(1)
            );

            Assert.AreEqual(RepositoryItemKind.Structure, item.Kind);
            Assert.IsNotNull(item.Structure);
            Assert.IsNull(item.Instruction);
        }

        [Test]
        public void From_ADirective_ProducesADirectiveItem()
        {
            RepositoryItem item = RepositoryItem.From(
                ProgressionFixtures.Directive(ProgressionFixtures.Overclock),
                new InstanceID(1)
            );

            Assert.AreEqual(RepositoryItemKind.Directive, item.Kind);
            Assert.IsNotNull(item.Directive);
        }

        [Test]
        public void From_ADependency_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = RepositoryItem.From(
                ProgressionFixtures.Dependency(ProgressionFixtures.StandardLibrary, 0),
                new InstanceID(1)
            ));
        }

        [Test]
        public void From_CarriesTheDefinitionsTags()
        {
            RepositoryItem item = RepositoryItem.From(
                ProgressionFixtures.Structure(ProgressionFixtures.RepeatTwo),
                new InstanceID(1)
            );

            Assert.AreEqual(2, item.Tags.Count);
            Assert.AreEqual("Structure", item.Tags[0]);
        }

        [Test]
        public void Acquire_AnInstruction_Succeeds()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();

            AcquisitionResult result = repository.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                AcquisitionOrigin.Purchase
            );

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(RepositoryRejection.None, result.Rejection);
            Assert.AreEqual(1, result.Entry.Suffix);
            Assert.AreEqual(AcquisitionOrigin.Purchase, result.Entry.Origin);
            Assert.IsFalse(result.Entry.IsStarterProtected);
        }

        [Test]
        public void Acquire_ADependency_IsRejectedAsNotAnItem()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();

            AcquisitionResult result = repository.Acquire(
                ProgressionFixtures.Dependency(ProgressionFixtures.StandardLibrary, 0),
                AcquisitionOrigin.Reward
            );

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(RepositoryRejection.NotAnItem, result.Rejection);
            Assert.AreEqual(0, repository.Entries.Count, "a rejection changes nothing.");
        }

        [Test]
        public void Acquire_WithStarterOrigin_MarksTheEntryProtected()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();

            AcquisitionResult result = repository.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                AcquisitionOrigin.Starter
            );

            Assert.IsTrue(result.Entry.IsStarterProtected);
        }

        [Test]
        public void Acquire_TheSameDefinitionTwice_AllocatesSuffixesOneThenTwo()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();

            AcquisitionResult first = Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            AcquisitionResult second = Acquire(repository, ProgressionFixtures.ValuePlusTwo);

            Assert.AreEqual(1, first.Entry.Suffix);
            Assert.AreEqual(2, second.Entry.Suffix);
        }

        [Test]
        public void Acquire_SuffixesAreAllocatedPerDefinition()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();

            Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            AcquisitionResult other = Acquire(repository, ProgressionFixtures.ValuePlusThree);

            Assert.AreEqual(1, other.Entry.Suffix, "a different definition starts its own numbering.");
        }

        [Test]
        public void Acquire_AllocatesADistinctInstanceIDEachTime()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();

            AcquisitionResult first = Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            AcquisitionResult second = Acquire(repository, ProgressionFixtures.ValuePlusTwo);

            Assert.AreNotEqual(first.Entry.Item.InstanceID, second.Entry.Item.InstanceID);
        }

        [Test]
        public void Entries_AreInAcquisitionOrder()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();

            Acquire(repository, ProgressionFixtures.ScorePlusValue);
            Acquire(repository, ProgressionFixtures.ValuePlusTwo);

            Assert.AreEqual(ProgressionFixtures.ScorePlusValue, repository.Entries[0].Item.DefinitionID);
            Assert.AreEqual(ProgressionFixtures.ValuePlusTwo, repository.Entries[1].Item.DefinitionID);
        }

        [Test]
        public void EntriesOf_ReturnsOnlyThatDefinitionsEntries()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();

            Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            Acquire(repository, ProgressionFixtures.ValuePlusThree);
            Acquire(repository, ProgressionFixtures.ValuePlusTwo);

            IReadOnlyList<RepositoryEntry> entries = repository.EntriesOf(ProgressionFixtures.ValuePlusTwo);

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(1, entries[0].Suffix);
            Assert.AreEqual(2, entries[1].Suffix);
        }

        [Test]
        public void Delete_AnOrdinaryEntry_Succeeds()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            AcquisitionResult acquired = Acquire(repository, ProgressionFixtures.ValuePlusTwo);

            DeletionResult result = repository.Delete(acquired.Entry.Item.InstanceID);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(0, repository.Entries.Count);
            Assert.IsFalse(repository.Contains(acquired.Entry.Item.InstanceID));
        }

        [Test]
        public void Delete_AStarterProtectedEntry_IsRejected()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            AcquisitionResult acquired = repository.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                AcquisitionOrigin.Starter
            );

            DeletionResult result = repository.Delete(acquired.Entry.Item.InstanceID);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(RepositoryRejection.StarterProtected, result.Rejection);
            Assert.AreEqual(1, repository.Entries.Count, "a rejection changes nothing.");
        }

        [Test]
        public void Delete_AnUnknownInstance_IsRejected()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();

            DeletionResult result = repository.Delete(new InstanceID(99));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(RepositoryRejection.UnknownInstance, result.Rejection);
        }

        [Test]
        public void Delete_DoesNotReuseTheSuffix()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            AcquisitionResult second = Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            repository.Delete(second.Entry.Item.InstanceID);

            AcquisitionResult third = Acquire(repository, ProgressionFixtures.ValuePlusTwo);

            Assert.AreEqual(3, third.Entry.Suffix, "a freed suffix is never renumbered or reissued.");
        }

        [Test]
        public void Delete_OfTheOnlyEntry_StillDoesNotResetNumbering()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            AcquisitionResult first = Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            repository.Delete(first.Entry.Item.InstanceID);

            AcquisitionResult second = Acquire(repository, ProgressionFixtures.ValuePlusTwo);

            Assert.AreEqual(2, second.Entry.Suffix);
        }

        [Test]
        public void Replace_WithADerivedItemOfTheSameIdentity_Succeeds()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            AcquisitionResult acquired = Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            InstanceID id = acquired.Entry.Item.InstanceID;
            RepositoryItem derived = RepositoryItem.From(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                id
            );

            ReplacementResult result = repository.Replace(id, derived);

            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(repository.TryGet(id, out RepositoryEntry entry));
            Assert.AreEqual(1, entry.Suffix, "replacement preserves the entry's suffix.");
        }

        [Test]
        public void Replace_WithADifferentIdentity_IsRejected()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            AcquisitionResult acquired = Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            RepositoryItem foreign = RepositoryItem.From(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                new InstanceID(77)
            );

            ReplacementResult result = repository.Replace(acquired.Entry.Item.InstanceID, foreign);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(RepositoryRejection.DefinitionMismatch, result.Rejection);
        }

        [Test]
        public void Replace_WithADifferentDefinition_IsRejected()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            AcquisitionResult acquired = Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            InstanceID id = acquired.Entry.Item.InstanceID;
            RepositoryItem other = RepositoryItem.From(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusThree),
                id
            );

            ReplacementResult result = repository.Replace(id, other);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(RepositoryRejection.DefinitionMismatch, result.Rejection);
        }

        [Test]
        public void Replace_AnUnknownInstance_IsRejected()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            RepositoryItem item = RepositoryItem.From(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                new InstanceID(99)
            );

            ReplacementResult result = repository.Replace(new InstanceID(99), item);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(RepositoryRejection.UnknownInstance, result.Rejection);
        }

        [Test]
        public void TryResolveContent_WithNothingExcluded_ReturnsTheLowestSuffix()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            Acquire(repository, ProgressionFixtures.ValuePlusTwo);

            bool found = repository.TryResolveContent(
                ProgressionFixtures.ValuePlusTwo,
                Array.Empty<InstanceID>(),
                out RepositoryEntry entry
            );

            Assert.IsTrue(found);
            Assert.AreEqual(1, entry.Suffix);
        }

        [Test]
        public void TryResolveContent_WithTheLowestExcluded_ReturnsTheNext()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            AcquisitionResult first = Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            Acquire(repository, ProgressionFixtures.ValuePlusTwo);

            bool found = repository.TryResolveContent(
                ProgressionFixtures.ValuePlusTwo,
                new[] { first.Entry.Item.InstanceID },
                out RepositoryEntry entry
            );

            Assert.IsTrue(found);
            Assert.AreEqual(2, entry.Suffix);
        }

        [Test]
        public void TryResolveContent_WhenEveryCopyIsExcluded_ReturnsFalse()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            AcquisitionResult first = Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            AcquisitionResult second = Acquire(repository, ProgressionFixtures.ValuePlusTwo);

            bool found = repository.TryResolveContent(
                ProgressionFixtures.ValuePlusTwo,
                new[] { first.Entry.Item.InstanceID, second.Entry.Item.InstanceID },
                out RepositoryEntry entry
            );

            Assert.IsFalse(found);
            Assert.IsNull(entry, "a failed resolution yields no entry, matching the catalog TryGet idiom.");
        }

        [Test]
        public void TryResolveContent_ForAnUnheldDefinition_ReturnsFalse()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();

            bool found = repository.TryResolveContent(
                ProgressionFixtures.ValuePlusTwo,
                Array.Empty<InstanceID>(),
                out _
            );

            Assert.IsFalse(found);
        }

        [Test]
        public void Records_AreInTransactionOrder()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            AcquisitionResult first = Acquire(repository, ProgressionFixtures.ValuePlusTwo);
            Acquire(repository, ProgressionFixtures.ValuePlusThree);
            repository.Delete(first.Entry.Item.InstanceID);

            Assert.AreEqual(3, repository.Records.Count);
            Assert.IsInstanceOf<AcquisitionRecord>(repository.Records[0]);
            Assert.IsInstanceOf<AcquisitionRecord>(repository.Records[1]);
            Assert.IsInstanceOf<DeletionRecord>(repository.Records[2]);
        }

        [Test]
        public void Records_AreNotWrittenForARejection()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();

            repository.Acquire(ProgressionFixtures.Dependency(ProgressionFixtures.StandardLibrary, 0), AcquisitionOrigin.Reward);
            repository.Delete(new InstanceID(99));

            Assert.AreEqual(0, repository.Records.Count);
        }

        [Test]
        public void Snapshot_DoesNotChangeWhenTheRepositoryDoes()
        {
            Repository repository = ProgressionFixtures.EmptyRepository();
            Acquire(repository, ProgressionFixtures.ValuePlusTwo);

            RepositorySnapshot snapshot = repository.Snapshot();
            Acquire(repository, ProgressionFixtures.ValuePlusThree);

            Assert.AreEqual(1, snapshot.Entries.Count);
            Assert.AreEqual(2, repository.Entries.Count);
        }

        /// <summary>
        /// Acquires one Instruction of the given definition as a purchase.
        /// </summary>
        /// <param name="repository">The Repository to acquire into.</param>
        /// <param name="definitionID">The Instruction definition's identity.</param>
        /// <returns>The acquisition result.</returns>
        private static AcquisitionResult Acquire(Repository repository, string definitionID)
        {
            return repository.Acquire(ProgressionFixtures.Instruction(definitionID), AcquisitionOrigin.Purchase);
        }
    }
}
