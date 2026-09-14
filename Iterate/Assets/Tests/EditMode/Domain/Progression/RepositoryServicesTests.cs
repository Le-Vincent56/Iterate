using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the two Repository Services the slice sells: Delete, which destroys the item's attached
    /// Patches with it and refunds nothing for them, and Duplicate, which produces a fresh unpatched
    /// instance under the next suffix. Starter items are protected from deletion but may be duplicated,
    /// so protection is asserted on exactly one of the two.
    /// </summary>
    public sealed class RepositoryServicesTests
    {
        private const string FixedNumberAdditionHosts = "FIXED_NUMBER_ADDITION_HOSTS";

        [Test]
        public void Delete_ChargesTheDeletePriceAndRemovesTheEntry()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(3);
            InstanceID item = PurchasedInstruction(session);
            RepositoryServices services = new(session);

            ServiceResult result = services.Delete(item);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
            Assert.IsFalse(session.Repository.TryGet(item, out RepositoryEntry _));
            Assert.AreEqual(3, session.Economy.Parameters.DeleteRepositoryItemPrice);
        }

        [Test]
        public void Delete_APatchedInstruction_RecordsOnePatchDestroyedPerAttachmentAndNoRefund()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(11);
            InstanceID item = PurchasedInstruction(session);
            PatchInstaller installer = new(session);
            installer.Attach(Attachment(item, 1, 4));
            installer.Attach(Attachment(item, 2, 4));
            int entriesBefore = session.Economy.Tokens.Entries.Count;
            RepositoryServices services = new(session);

            ServiceResult result = services.Delete(item);

            int destroyed = 0;
            for (int i = 0; i < result.Record.Consequences.Count; i++)
            {
                if (result.Record.Consequences[i] is PatchDestroyed)
                    destroyed++;
            }

            Assert.AreEqual(2, destroyed);
            Assert.AreEqual(entriesBefore + 1, session.Economy.Tokens.Entries.Count);
            Assert.IsTrue(session.Economy.Tokens.Entries[entriesBefore].IsDebit);
            Assert.AreEqual(TokenBasis.ServicePrice, session.Economy.Tokens.Entries[entriesBefore].Basis);
        }

        [Test]
        public void Delete_RecordsTokenThenPatchesThenTheDeletion()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(7);
            InstanceID item = PurchasedInstruction(session);
            new PatchInstaller(session).Attach(Attachment(item, 1, 4));
            RepositoryServices services = new(session);

            ServiceResult result = services.Delete(item);

            Assert.AreEqual(TransactionKind.ItemDeletion, result.Record.Kind);
            Assert.AreEqual(3, result.Record.Consequences.Count);
            Assert.IsInstanceOf<TokenConsequence>(result.Record.Consequences[0]);
            Assert.IsInstanceOf<PatchDestroyed>(result.Record.Consequences[1]);
            Assert.IsInstanceOf<ItemDeleted>(result.Record.Consequences[2]);
        }

        [Test]
        public void Delete_AStarterItem_RejectsStarterProtectedAndChargesNothing()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(3);
            InstanceID starter = session.Repository.Entries[0].Item.InstanceID;
            RepositoryServices services = new(session);

            ServiceResult result = services.Delete(starter);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ServiceRejection.StarterProtected, result.Rejection);
            Assert.AreEqual(new TokenAmount(3), session.Economy.Tokens.Balance);
            Assert.IsTrue(session.Repository.TryGet(starter, out RepositoryEntry _));
            Assert.AreEqual(0, session.Economy.Records.Count);
        }

        [Test]
        public void Delete_WithoutEnoughTokens_RejectsAndChangesNothing()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(2);
            InstanceID item = PurchasedInstruction(session);
            RepositoryServices services = new(session);
            int entries = session.Repository.Entries.Count;
            int ledgerEntries = session.Economy.Tokens.Entries.Count;

            ServiceResult result = services.Delete(item);

            Assert.AreEqual(ServiceRejection.InsufficientTokens, result.Rejection);
            Assert.AreEqual(new TokenAmount(2), session.Economy.Tokens.Balance);
            Assert.AreEqual(entries, session.Repository.Entries.Count);
            Assert.AreEqual(ledgerEntries, session.Economy.Tokens.Entries.Count);
            Assert.AreEqual(0, session.Economy.Records.Count);
        }

        [Test]
        public void Delete_AnUnknownInstance_RejectsUnknownInstance()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(3);
            RepositoryServices services = new(session);

            ServiceResult result = services.Delete(new InstanceID(9999));

            Assert.AreEqual(ServiceRejection.UnknownInstance, result.Rejection);
            Assert.AreEqual(new TokenAmount(3), session.Economy.Tokens.Balance);
        }

        [Test]
        public void Duplicate_ChargesTheDuplicatePriceAndAcquiresANewEntry()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(7);
            InstanceID item = PurchasedInstruction(session);
            int before = session.Repository.Entries.Count;
            RepositoryServices services = new(session);

            ServiceResult result = services.Duplicate(item);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
            Assert.AreEqual(before + 1, session.Repository.Entries.Count);
            Assert.AreEqual(7, session.Economy.Parameters.DuplicateRepositoryItemPrice);
            Assert.AreEqual(AcquisitionOrigin.Duplicate, result.Entry.Origin);
        }

        [Test]
        public void Duplicate_TakesTheNextSuffixAndLeavesTheOriginalUntouched()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(7);
            InstanceID item = PurchasedInstruction(session);
            session.Repository.TryGet(item, out RepositoryEntry original);
            RepositoryServices services = new(session);

            ServiceResult result = services.Duplicate(item);

            Assert.AreEqual(original.Item.DefinitionID, result.Entry.Item.DefinitionID);
            Assert.AreEqual(original.Suffix + 1, result.Entry.Suffix);
            Assert.AreNotEqual(original.Item.InstanceID, result.Entry.Item.InstanceID);
            session.Repository.TryGet(item, out RepositoryEntry after);
            Assert.AreEqual(original.Suffix, after.Suffix);
            Assert.AreEqual(original.Origin, after.Origin);
        }

        [Test]
        public void Duplicate_ProducesAnUnpatchedCopy()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(11);
            InstanceID item = PurchasedInstruction(session);
            new PatchInstaller(session).Attach(Attachment(item, 1, 4));
            RepositoryServices services = new(session);

            ServiceResult result = services.Duplicate(item);

            Assert.AreEqual(0, result.Entry.Item.Instruction.AttachedPatches.Count);
            session.Repository.TryGet(item, out RepositoryEntry original);
            Assert.AreEqual(1, original.Item.Instruction.AttachedPatches.Count);
        }

        [Test]
        public void Duplicate_AStarterItem_IsAllowed()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(7);
            InstanceID starter = session.Repository.Entries[0].Item.InstanceID;
            RepositoryServices services = new(session);

            ServiceResult result = services.Duplicate(starter);

            Assert.IsTrue(result.Succeeded);
            Assert.IsFalse(result.Entry.IsStarterProtected);
            Assert.AreEqual(AcquisitionOrigin.Duplicate, result.Entry.Origin);
        }

        [Test]
        public void Duplicate_RecordsTokenThenTheAcquisition()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(7);
            RepositoryServices services = new(session);

            ServiceResult result = services.Duplicate(session.Repository.Entries[0].Item.InstanceID);

            Assert.AreEqual(TransactionKind.ItemDuplication, result.Record.Kind);
            Assert.AreEqual(2, result.Record.Consequences.Count);
            Assert.IsInstanceOf<TokenConsequence>(result.Record.Consequences[0]);
            Assert.IsInstanceOf<ItemAcquired>(result.Record.Consequences[1]);
        }

        [Test]
        public void Duplicate_WithoutEnoughTokens_RejectsAndChangesNothing()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(6);
            int before = session.Repository.Entries.Count;
            RepositoryServices services = new(session);

            ServiceResult result = services.Duplicate(session.Repository.Entries[0].Item.InstanceID);

            Assert.AreEqual(ServiceRejection.InsufficientTokens, result.Rejection);
            Assert.AreEqual(new TokenAmount(6), session.Economy.Tokens.Balance);
            Assert.AreEqual(before, session.Repository.Entries.Count);
            Assert.AreEqual(0, session.Economy.Records.Count);
        }

        [Test]
        public void Duplicate_AnUnknownInstance_RejectsUnknownInstance()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(7);
            RepositoryServices services = new(session);

            ServiceResult result = services.Duplicate(new InstanceID(9999));

            Assert.AreEqual(ServiceRejection.UnknownInstance, result.Rejection);
        }

        [Test]
        public void PreviewDelete_MatchesWhatDeleteThenCommits()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(11);
            InstanceID item = PurchasedInstruction(session);
            PatchInstaller installer = new(session);
            installer.Attach(Attachment(item, 1, 4));
            installer.Attach(Attachment(item, 2, 4));
            RepositoryServices services = new(session);

            ServicePreview preview = services.PreviewDelete(item);
            services.Delete(item);

            Assert.AreEqual(ServiceRejection.None, preview.Rejection);
            Assert.AreEqual(3, preview.Price);
            Assert.AreEqual(2, preview.PatchesDestroyed);
            Assert.AreEqual(session.Economy.Tokens.Balance, preview.TokensAfter);
        }

        [Test]
        public void PreviewDuplicate_MatchesWhatDuplicateThenCommits()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(7);
            RepositoryServices services = new(session);
            InstanceID item = session.Repository.Entries[0].Item.InstanceID;

            ServicePreview preview = services.PreviewDuplicate(item);
            services.Duplicate(item);

            Assert.AreEqual(ServiceRejection.None, preview.Rejection);
            Assert.AreEqual(7, preview.Price);
            Assert.AreEqual(0, preview.PatchesDestroyed);
            Assert.AreEqual(session.Economy.Tokens.Balance, preview.TokensAfter);
        }

        [Test]
        public void PreviewDelete_OfAStarterItem_CarriesTheRejectionAndCommitsNothing()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(3);
            RepositoryServices services = new(session);

            ServicePreview preview = services.PreviewDelete(session.Repository.Entries[0].Item.InstanceID);

            Assert.AreEqual(ServiceRejection.StarterProtected, preview.Rejection);
            Assert.AreEqual(0, session.Economy.Records.Count);
            Assert.AreEqual(new TokenAmount(3), session.Economy.Tokens.Balance);
        }

        private static PatchAttachmentRequest Attachment(InstanceID host, int socket, int price)
        {
            return new PatchAttachmentRequest(
                EconomyFixtures.PatchWithRule("WB-PAT-001", FixedNumberAdditionHosts),
                host,
                socket,
                price,
                null,
                "OFF-100");
        }

        private static InstanceID PurchasedInstruction(SessionState session)
        {
            AcquisitionResult acquired = session.Repository.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                AcquisitionOrigin.Purchase);

            return acquired.Entry.Item.InstanceID;
        }
    }
}
