using System;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the Session factory: seeding the Repository from a Starter Archetype in authored order
    /// through the catalog's single item seam, installing the starter Dependency, and exposing the
    /// caller-owned identity source, the Session-scoped ordinal tracker and the Session identities.
    /// </summary>
    public sealed class SessionStateTests
    {
        [Test]
        public void Create_SeedsTheRepositoryInArchetypeOrder()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.AreEqual(3, session.Repository.Entries.Count);
            Assert.AreEqual(ProgressionFixtures.ValuePlusTwo, session.Repository.Entries[0].Item.DefinitionID);
            Assert.AreEqual(ProgressionFixtures.ValuePlusTwo, session.Repository.Entries[1].Item.DefinitionID);
            Assert.AreEqual(ProgressionFixtures.ScorePlusValue, session.Repository.Entries[2].Item.DefinitionID);
        }

        [Test]
        public void Create_NumbersTheDuplicateStarterOneThenTwo()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.AreEqual(1, session.Repository.Entries[0].Suffix);
            Assert.AreEqual(2, session.Repository.Entries[1].Suffix);
            Assert.AreEqual(1, session.Repository.Entries[2].Suffix, "a different definition starts its own numbering.");
        }

        [Test]
        public void Create_MarksEveryStarterEntryProtected()
        {
            SessionState session = ProgressionFixtures.Session();

            foreach (RepositoryEntry entry in session.Repository.Entries)
            {
                Assert.AreEqual(AcquisitionOrigin.Starter, entry.Origin);
                Assert.IsTrue(entry.IsStarterProtected, entry.Item.DefinitionID);
            }
        }

        [Test]
        public void Create_SeedsStructuresAndDirectivesThroughTheSameSeam()
        {
            SessionState session = SessionState.Create(
                ProgressionFixtures.Catalog(),
                ProgressionFixtures.Archetype(ProgressionFixtures.RepeatTwo, ProgressionFixtures.Overclock),
                "WB-SYS-001",
                "session-1",
                "seed-1"
            );

            Assert.AreEqual(RepositoryItemKind.Structure, session.Repository.Entries[0].Item.Kind);
            Assert.AreEqual(RepositoryItemKind.Directive, session.Repository.Entries[1].Item.Kind);
        }

        [Test]
        public void Create_ExposesTheStarterDependencyInstance()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.IsNotNull(session.StarterDependency);
            Assert.AreEqual(ProgressionFixtures.StandardLibrary, session.StarterDependency.Definition.ID.Value);
            Assert.AreEqual(0, session.StarterDependency.Definition.RAM);
        }

        [Test]
        public void Create_DoesNotPutTheStarterDependencyInTheRepository()
        {
            SessionState session = ProgressionFixtures.Session();

            foreach (RepositoryEntry entry in session.Repository.Entries)
            {
                Assert.AreNotEqual(ProgressionFixtures.StandardLibrary, entry.Item.DefinitionID);
            }
        }

        [Test]
        public void Create_WithARAMConsumingStarterDependency_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = SessionState.Create(
                ProgressionFixtures.Catalog(),
                ProgressionFixtures.ArchetypeWithRAMConsumingStarter(),
                "WB-SYS-001",
                "session-1",
                "seed-1"
            ));
        }

        [Test]
        public void Create_WithAnUnresolvableStartingItem_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = SessionState.Create(
                ProgressionFixtures.Catalog(),
                ProgressionFixtures.Archetype("WB-INS-404"),
                "WB-SYS-001",
                "session-1",
                "seed-1"
            ));
        }

        [Test]
        public void Create_WithANonItemStartingEntry_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = SessionState.Create(
                ProgressionFixtures.Catalog(),
                ProgressionFixtures.Archetype(ProgressionFixtures.CleanBuild),
                "WB-SYS-001",
                "session-1",
                "seed-1"
            ));
        }

        [Test]
        public void Create_ExposesTheSessionIdentities()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.AreEqual("WB-SYS-001", session.SystemIdentity);
            Assert.AreEqual("session-1", session.SessionIdentity);
            Assert.AreEqual("seed-1", session.SessionSeedIdentity);
            Assert.AreEqual("fixture-0.0.1", session.CatalogRevision);
        }

        [Test]
        public void Create_ExposesAnIdentitySourceThatContinuesFromTheSeeding()
        {
            SessionState session = ProgressionFixtures.Session();

            InstanceID next = session.InstanceIDs.Next();

            Assert.AreEqual(5, next.Value, "the three starter items took 1-3 and the starter Dependency took 4.");
        }

        [Test]
        public void Create_ExposesASessionScopedOrdinalTracker()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.IsNotNull(session.Ordinals);
            Assert.AreEqual(1, session.Ordinals.Next("Shop offer", session.SessionIdentity));
            Assert.AreEqual(2, session.Ordinals.Next("Shop offer", session.SessionIdentity));
        }

        [Test]
        public void Create_GivesEachSessionItsOwnRepository()
        {
            SessionState first = ProgressionFixtures.Session();
            SessionState second = ProgressionFixtures.Session();

            first.Repository.Acquire(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusThree),
                AcquisitionOrigin.Purchase
            );

            Assert.AreEqual(4, first.Repository.Entries.Count);
            Assert.AreEqual(3, second.Repository.Entries.Count);
        }

        [Test]
        public void Create_ExposesAnUnconfirmedActiveBranch()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.IsNotNull(session.ActiveBranch);
            Assert.IsNull(session.ActiveBranch.Confirmed, "a Session begins with no confirmed Branch.");
            Assert.IsFalse(session.ActiveBranch.HasConfigurationInProgress);
        }

        [Test]
        public void Create_GivesEachSessionItsOwnActiveBranch()
        {
            SessionState first = ProgressionFixtures.Session();
            SessionState second = ProgressionFixtures.Session();

            first.ActiveBranch.BeginConfiguration(
                new BranchConstraints(9, System.Array.Empty<string>(), System.Array.Empty<string>(), System.Array.Empty<string>(), System.Array.Empty<string>()),
                first.Repository.Snapshot()
            );

            Assert.IsTrue(first.ActiveBranch.HasConfigurationInProgress);
            Assert.IsFalse(second.ActiveBranch.HasConfigurationInProgress);
        }

        [Test]
        public void Create_OpensTheEconomyAtZeroTokens()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.IsNotNull(session.Economy);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
        }

        [Test]
        public void Create_SizesTheDependencyRackFromTheCatalogRegister()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.AreEqual(4, session.Economy.Dependencies.Capacity);
            Assert.AreEqual(0, session.Economy.Dependencies.Usage);
        }

        [Test]
        public void Create_InstallsTheStarterDependencyInTheRack()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.AreEqual(ProgressionFixtures.StandardLibrary, session.Economy.Dependencies.Starter.Definition.ID.Value);
            Assert.AreEqual(1, session.Economy.Dependencies.AllInstalled.Count);
        }

        [Test]
        public void StarterDependency_ForwardsToTheRacksStarter()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.AreSame(session.Economy.Dependencies.Starter, session.StarterDependency);
        }

        [Test]
        public void Create_GivesEachSessionItsOwnEconomy()
        {
            SessionState first = ProgressionFixtures.Session();
            SessionState second = ProgressionFixtures.Session();

            first.Economy.Tokens.Credit(new TokenAmount(4), TokenBasis.RewardTokens, "reward");

            Assert.AreEqual(new TokenAmount(4), first.Economy.Tokens.Balance);
            Assert.AreEqual(new TokenAmount(0), second.Economy.Tokens.Balance);
        }

        [Test]
        public void Create_RecordsTheStarterSeedingAsAcquisitions()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.AreEqual(3, session.Repository.Records.Count);
            Assert.IsInstanceOf<AcquisitionRecord>(session.Repository.Records[0]);
        }
    }
}
