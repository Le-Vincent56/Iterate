using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests Patch attachment as a validate-then-commit transaction: the whole request is
    /// validated before a single Token moves, a rejection leaves ledger, Repository and grants
    /// byte-for-byte as they were, and the preview a caller reads is computed by the same validation
    /// the commit runs. Replacement is the interesting case — it debits twice and destroys a Patch.
    /// </summary>
    public sealed class PatchInstallerTests
    {
        private const string ConstantPatch = "WB-PAT-001";

        private const string FixedNumberAdditionHosts = "FIXED_NUMBER_ADDITION_HOSTS";

        [Test]
        public void Attach_ToAnEmptySocket_DebitsThePriceAndSocketsThePatch()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(4);
            InstanceID host = FirstInstruction(session);
            PatchInstaller installer = new(session);

            PatchAttachmentResult result = installer.Attach(Request(session, host, 1, 4));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
            Assert.IsTrue(session.Repository.TryGet(host, out RepositoryEntry entry));
            Assert.AreEqual(1, entry.Item.Instruction.AttachedPatches.Count);
            Assert.AreEqual(1, entry.Item.Instruction.AttachedPatches[0].Socket);
            Assert.AreEqual(result.Attached.InstanceID, entry.Item.Instruction.AttachedPatches[0].Patch.InstanceID);
        }

        [Test]
        public void Attach_RecordsOneTransactionWithItsConsequences()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(4);
            InstanceID host = FirstInstruction(session);
            PatchInstaller installer = new(session);

            PatchAttachmentResult result = installer.Attach(Request(session, host, 1, 4));

            Assert.AreEqual(TransactionKind.PatchAttachment, result.Record.Kind);
            Assert.AreEqual(1, session.Economy.Records.Count);
            Assert.AreEqual(2, result.Record.Consequences.Count);
            Assert.IsInstanceOf<TokenConsequence>(result.Record.Consequences[0]);
            Assert.IsInstanceOf<PatchAttached>(result.Record.Consequences[1]);
        }

        [Test]
        public void Attach_TwoSocketsOfOneDefinition_AreBothLegal()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(8);
            InstanceID host = FirstInstruction(session);
            PatchInstaller installer = new(session);
            installer.Attach(Request(session, host, 1, 4));

            PatchAttachmentResult second = installer.Attach(Request(session, host, 2, 4));

            Assert.IsTrue(second.Succeeded);
            session.Repository.TryGet(host, out RepositoryEntry entry);
            Assert.AreEqual(2, entry.Item.Instruction.AttachedPatches.Count);
            Assert.AreNotEqual(
                entry.Item.Instruction.AttachedPatches[0].Patch.InstanceID,
                entry.Item.Instruction.AttachedPatches[1].Patch.InstanceID);
        }

        [Test]
        public void Attach_BeyondTheSocketCount_RejectsSocketOutOfRange()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(4);
            InstanceID host = FirstInstruction(session);
            PatchInstaller installer = new(session);

            PatchAttachmentResult result = installer.Attach(Request(session, host, 3, 4));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(PatchAttachmentRejection.SocketOutOfRange, result.Rejection);
            Assert.AreEqual(new TokenAmount(4), session.Economy.Tokens.Balance);
            Assert.AreEqual(2, session.Economy.Parameters.PatchSocketsPerRepositoryInstance);
        }

        [Test]
        public void Attach_SocketZero_RejectsSocketOutOfRange()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(4);
            PatchInstaller installer = new(session);

            PatchAttachmentResult result = installer.Attach(Request(session, FirstInstruction(session), 0, 4));

            Assert.AreEqual(PatchAttachmentRejection.SocketOutOfRange, result.Rejection);
        }

        [Test]
        public void Attach_ToAnUnknownHost_RejectsUnknownHost()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(4);
            PatchInstaller installer = new(session);

            PatchAttachmentResult result = installer.Attach(Request(session, new InstanceID(9999), 1, 4));

            Assert.AreEqual(PatchAttachmentRejection.UnknownHost, result.Rejection);
            Assert.AreEqual(new TokenAmount(4), session.Economy.Tokens.Balance);
        }

        [Test]
        public void Attach_ToAnIneligibleHost_RejectsBeforeTokensAreConsidered()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(0);
            InstanceID scoreHost = AcquireScoreInstruction(session);
            PatchInstaller installer = new(session);

            PatchAttachmentResult result = installer.Attach(Request(session, scoreHost, 1, 4));

            Assert.AreEqual(PatchAttachmentRejection.HostIneligible, result.Rejection);
        }

        [Test]
        public void Attach_WithoutEnoughTokens_RejectsAndChangesNothing()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(3);
            InstanceID host = FirstInstruction(session);
            PatchInstaller installer = new(session);
            int entriesBefore = session.Economy.Tokens.Entries.Count;
            int recordsBefore = session.Economy.Records.Count;

            PatchAttachmentResult result = installer.Attach(Request(session, host, 1, 4));

            Assert.AreEqual(PatchAttachmentRejection.InsufficientTokens, result.Rejection);
            Assert.AreEqual(new TokenAmount(3), session.Economy.Tokens.Balance);
            Assert.AreEqual(entriesBefore, session.Economy.Tokens.Entries.Count);
            Assert.AreEqual(recordsBefore, session.Economy.Records.Count);
            session.Repository.TryGet(host, out RepositoryEntry entry);
            Assert.AreEqual(0, entry.Item.Instruction.AttachedPatches.Count);
        }

        [Test]
        public void Attach_IntoAnOccupiedSocket_DebitsPriceAndFeeAsTwoEntries()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(13);
            InstanceID host = FirstInstruction(session);
            PatchInstaller installer = new(session);
            installer.Attach(Request(session, host, 1, 4));
            int entriesBefore = session.Economy.Tokens.Entries.Count;

            PatchAttachmentResult result = installer.Attach(Request(session, host, 1, 6));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(entriesBefore + 2, session.Economy.Tokens.Entries.Count);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
            Assert.AreEqual(TokenBasis.OfferPrice, session.Economy.Tokens.Entries[entriesBefore].Basis);
            Assert.AreEqual(TokenBasis.ReplacementFee, session.Economy.Tokens.Entries[entriesBefore + 1].Basis);
            Assert.AreEqual(new TokenAmount(3), session.Economy.Tokens.Entries[entriesBefore + 1].Amount);
        }

        [Test]
        public void Attach_IntoAnOccupiedSocket_RecordsThePriorPatchAsDestroyed()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(13);
            InstanceID host = FirstInstruction(session);
            PatchInstaller installer = new(session);
            PatchAttachmentResult first = installer.Attach(Request(session, host, 1, 4));

            PatchAttachmentResult replacement = installer.Attach(Request(session, host, 1, 6));

            PatchDestroyed destroyed = null;
            for (int i = 0; i < replacement.Record.Consequences.Count; i++)
            {
                if (replacement.Record.Consequences[i] is PatchDestroyed candidate)
                    destroyed = candidate;
            }

            Assert.IsNotNull(destroyed);
            Assert.AreEqual(first.Attached.InstanceID, destroyed.Patch);
            Assert.AreEqual(1, destroyed.Socket);
            session.Repository.TryGet(host, out RepositoryEntry entry);
            Assert.AreEqual(1, entry.Item.Instruction.AttachedPatches.Count);
            Assert.AreEqual(replacement.Attached.InstanceID, entry.Item.Instruction.AttachedPatches[0].Patch.InstanceID);
            Assert.AreNotEqual(first.Attached.InstanceID, replacement.Attached.InstanceID);
        }

        [Test]
        public void Attach_IntoAnOccupiedSocket_KeepsTheOtherSocket()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(17);
            InstanceID host = FirstInstruction(session);
            PatchInstaller installer = new(session);
            installer.Attach(Request(session, host, 1, 4));
            PatchAttachmentResult second = installer.Attach(Request(session, host, 2, 4));

            installer.Attach(Request(session, host, 1, 6));

            session.Repository.TryGet(host, out RepositoryEntry entry);
            Assert.AreEqual(2, entry.Item.Instruction.AttachedPatches.Count);
            Assert.AreEqual(second.Attached.InstanceID, entry.Item.Instruction.AttachedPatches[1].Patch.InstanceID);
        }

        [Test]
        public void Attach_ReplacementWithoutEnoughForTheFee_RejectsAndChangesNothing()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(12);
            InstanceID host = FirstInstruction(session);
            PatchInstaller installer = new(session);
            PatchAttachmentResult first = installer.Attach(Request(session, host, 1, 4));
            TokenAmount balanceBefore = session.Economy.Tokens.Balance;
            int entriesBefore = session.Economy.Tokens.Entries.Count;

            PatchAttachmentResult result = installer.Attach(Request(session, host, 1, 6));

            Assert.AreEqual(PatchAttachmentRejection.InsufficientTokens, result.Rejection);
            Assert.AreEqual(balanceBefore, session.Economy.Tokens.Balance);
            Assert.AreEqual(entriesBefore, session.Economy.Tokens.Entries.Count);
            session.Repository.TryGet(host, out RepositoryEntry entry);
            Assert.AreEqual(first.Attached.InstanceID, entry.Item.Instruction.AttachedPatches[0].Patch.InstanceID);
        }

        [Test]
        public void Attach_WithAGrant_CostsNothingAndConsumesTheGrant()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(0);
            InstanceID host = FirstInstruction(session);
            PatchGrant grant = session.Economy.Grant(EconomyFixtures.PatchWithRule(ConstantPatch, FixedNumberAdditionHosts), false, "WB-RWD-002:1");
            PatchInstaller installer = new(session);

            PatchAttachmentResult result = installer.Attach(new PatchAttachmentRequest(
                EconomyFixtures.PatchWithRule(ConstantPatch, FixedNumberAdditionHosts),
                host,
                1,
                0,
                grant.GrantIdentity,
                "WB-RWD-002:1"));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(0, session.Economy.PatchGrants.Count);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
        }

        [Test]
        public void Attach_WithAnUnknownGrant_RejectsUnknownGrant()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(0);
            PatchInstaller installer = new(session);

            PatchAttachmentResult result = installer.Attach(new PatchAttachmentRequest(
                EconomyFixtures.PatchWithRule(ConstantPatch, FixedNumberAdditionHosts),
                FirstInstruction(session),
                1,
                0,
                new InstanceID(9999),
                "grant"));

            Assert.AreEqual(PatchAttachmentRejection.UnknownGrant, result.Rejection);
        }

        [Test]
        public void Attach_WithAGrantForAnotherDefinition_RejectsGrantDefinitionMismatch()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(0);
            PatchGrant grant = session.Economy.Grant(EconomyFixtures.PatchWithRule("WB-PAT-002", FixedNumberAdditionHosts), false, "basis");
            PatchInstaller installer = new(session);

            PatchAttachmentResult result = installer.Attach(new PatchAttachmentRequest(
                EconomyFixtures.PatchWithRule(ConstantPatch, FixedNumberAdditionHosts),
                FirstInstruction(session),
                1,
                0,
                grant.GrantIdentity,
                "basis"));

            Assert.AreEqual(PatchAttachmentRejection.GrantDefinitionMismatch, result.Rejection);
            Assert.AreEqual(1, session.Economy.PatchGrants.Count);
        }

        [Test]
        public void Attach_ToAStructureEntry_RejectsHostNotAnInstruction()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(4);
            session.Repository.Acquire(ProgressionFixtures.Structure(ProgressionFixtures.RepeatTwo), AcquisitionOrigin.Purchase);
            InstanceID structureEntry = session.Repository.Entries[session.Repository.Entries.Count - 1].Item.InstanceID;
            PatchInstaller installer = new(session);

            PatchAttachmentResult result = installer.Attach(Request(session, structureEntry, 1, 4));

            Assert.AreEqual(PatchAttachmentRejection.HostNotAnInstruction, result.Rejection);
        }

        [Test]
        public void EligibleHosts_ListsOnlyTheEntriesTheRuleAccepts()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(0);
            InstanceID scoreHost = AcquireScoreInstruction(session);
            PatchInstaller installer = new(session);

            IReadOnlyList<RepositoryEntry> eligible = installer.EligibleHosts(EconomyFixtures.PatchWithRule(ConstantPatch, FixedNumberAdditionHosts));

            Assert.AreEqual(4, session.Repository.Entries.Count);
            Assert.AreEqual(3, eligible.Count);
            for (int i = 0; i < eligible.Count; i++)
            {
                Assert.AreNotEqual(scoreHost, eligible[i].Item.InstanceID);
            }
        }

        [Test]
        public void Preview_MatchesWhatAttachThenCommits()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(13);
            InstanceID host = FirstInstruction(session);
            PatchInstaller installer = new(session);
            installer.Attach(Request(session, host, 1, 4));
            PatchAttachmentRequest replacement = Request(session, host, 1, 6);

            PatchAttachmentPreview preview = installer.Preview(replacement);
            TokenAmount before = session.Economy.Tokens.Balance;
            installer.Attach(replacement);

            Assert.AreEqual(PatchAttachmentRejection.None, preview.Rejection);
            Assert.IsTrue(preview.IsReplacement);
            Assert.AreEqual(6, preview.Price);
            Assert.AreEqual(3, preview.Fee);
            Assert.AreEqual(before, preview.TokensBefore);
            Assert.AreEqual(session.Economy.Tokens.Balance, preview.TokensAfter);
        }

        [Test]
        public void Preview_OfARejectedRequest_CarriesTheRejectionAndCommitsNothing()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(0);
            PatchInstaller installer = new(session);

            PatchAttachmentPreview preview = installer.Preview(Request(session, FirstInstruction(session), 1, 4));

            Assert.AreEqual(PatchAttachmentRejection.InsufficientTokens, preview.Rejection);
            Assert.AreEqual(0, session.Economy.Records.Count);
            Assert.AreEqual(0, session.Economy.Tokens.Entries.Count);
        }

        private static PatchAttachmentRequest Request(
            SessionState session,
            InstanceID host,
            int socket,
            int price
        )
        {
            return new PatchAttachmentRequest(
                EconomyFixtures.PatchWithRule(ConstantPatch, FixedNumberAdditionHosts),
                host,
                socket,
                price,
                null,
                "OFF-100");
        }

        private static InstanceID AcquireScoreInstruction(SessionState session)
        {
            AcquisitionResult acquired = session.Repository.Acquire(
                EconomyFixtures.ScoreInstruction("WB-INS-012"),
                AcquisitionOrigin.Purchase);

            return acquired.Entry.Item.InstanceID;
        }

        private static InstanceID FirstInstruction(SessionState session)
        {
            return InstructionOf(session, ProgressionFixtures.ValuePlusTwo);
        }

        private static InstanceID InstructionOf(SessionState session, string definitionID)
        {
            IReadOnlyList<RepositoryEntry> entries = session.Repository.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Item.DefinitionID == definitionID)
                    return entries[i].Item.InstanceID;
            }

            Assert.Fail("the fixture Session carries no " + definitionID);
            return default;
        }
    }
}
