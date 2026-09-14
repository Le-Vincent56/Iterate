using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests reward resolution in its two phases. Plan is pure: it resolves every granted component to
    /// a concrete consequence or rejects the whole package, so a package naming content the catalog
    /// cannot supply credits nothing. Apply commits the immediates in authored order and hands back the
    /// choices still owed. A resolution stays incomplete until every non-cached grant is spent.
    /// </summary>
    public sealed class RewardResolverTests
    {
        private const string PoolID = "WB-POOL-101";
        private const string DependencyPoolID = "WB-POOL-102";
        private const string PackageID = "WB-RWD-101";

        [Test]
        public void Plan_AtNoTier_IsEmptyAndSucceeds()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);

            RewardPlanResult planned = RewardResolver.Plan(Package(), null, session, catalog);

            Assert.IsTrue(planned.Succeeded);
            Assert.AreEqual(0, planned.Plan.Planned.Count);
        }

        [Test]
        public void Plan_AtPass_ListsOnlyPassComponents()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);

            RewardPlanResult planned = RewardResolver.Plan(Package(), RewardTier.Pass, session, catalog);

            // The fixture package authors two Pass components — 4 Tokens and a 2-Token route bonus —
            // so "only Pass" means both of those and neither of the Optimize or Benchmark ones.
            Assert.IsTrue(planned.Succeeded);
            Assert.AreEqual(2, planned.Plan.Planned.Count);
            for (int i = 0; i < planned.Plan.Planned.Count; i++)
            {
                Assert.AreEqual(RewardTier.Pass, planned.Plan.Planned[i].Component.Tier);
            }

            Assert.AreEqual(PlannedRewardKind.TokenCredit, planned.Plan.Planned[0].Kind);
            Assert.AreEqual(TokenBasis.RewardTokens, planned.Plan.Planned[0].Basis);
            Assert.AreEqual(4, planned.Plan.Planned[0].Amount);
            Assert.AreEqual(TokenBasis.RouteBonus, planned.Plan.Planned[1].Basis);
            Assert.AreEqual(2, planned.Plan.Planned[1].Amount);
        }

        [Test]
        public void Plan_AtBenchmark_ListsEveryTierInAuthoredOrder()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);

            RewardPlanResult planned = RewardResolver.Plan(Package(), RewardTier.Benchmark, session, catalog);

            Assert.AreEqual(5, planned.Plan.Planned.Count);
            Assert.AreEqual(0, planned.Plan.Planned[0].ComponentIndex);
            Assert.AreEqual(1, planned.Plan.Planned[1].ComponentIndex);
            Assert.AreEqual(2, planned.Plan.Planned[2].ComponentIndex);
            Assert.AreEqual(3, planned.Plan.Planned[3].ComponentIndex);
            Assert.AreEqual(4, planned.Plan.Planned[4].ComponentIndex);
        }

        [Test]
        public void Plan_BenchmarkTokens_CarryTheBenchmarkBasis()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);

            RewardPlanResult planned = RewardResolver.Plan(Package(), RewardTier.Benchmark, session, catalog);

            Assert.AreEqual(TokenBasis.BenchmarkBonus, planned.Plan.Planned[4].Basis);
        }

        [Test]
        public void Plan_MissingContent_RejectsTheWholePlanAndChangesNothing()
        {
            SessionState session = Session(out ContentCatalog catalog, 5);
            RewardPackageDefinition package = PackageOf(
                Component(RewardTier.Pass, RewardComponentKind.Tokens, 4, null),
                Component(RewardTier.Pass, RewardComponentKind.GuaranteedContent, null, "WB-INS-999"));

            RewardPlanResult planned = RewardResolver.Plan(package, RewardTier.Pass, session, catalog);

            Assert.IsFalse(planned.Succeeded);
            Assert.AreEqual(RewardRejection.ContentMissing, planned.Rejection);
            Assert.AreEqual(new TokenAmount(5), session.Economy.Tokens.Balance);
            Assert.AreEqual(1, session.Economy.Tokens.Entries.Count);
            Assert.AreEqual(3, session.Repository.Entries.Count);
            Assert.AreEqual(0, session.Economy.Records.Count);
        }

        [Test]
        public void Plan_AMissingPool_RejectsPoolMissing()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);
            RewardPackageDefinition package = PackageOf(
                Component(RewardTier.Pass, RewardComponentKind.PoolChoice, null, "WB-POOL-999"));

            Assert.AreEqual(RewardRejection.PoolMissing, RewardResolver.Plan(package, RewardTier.Pass, session, catalog).Rejection);
        }

        [Test]
        public void Plan_ARAMSetBelowUsage_RejectsCapacityBelowUsage()
        {
            SessionState session = Session(out ContentCatalog catalog, 20);
            new DependencyInstaller(session).Install(new DependencyInstallationRequest(
                EconomyFixtures.Dependency(EconomyFixtures.TwoRAMDependency, 2),
                8,
                DependencyOrigin.Purchase,
                new InstanceID[0],
                "setup"));
            RewardPackageDefinition package = PackageOf(
                Component(RewardTier.Pass, RewardComponentKind.RAMSet, 1, null));

            RewardPlanResult planned = RewardResolver.Plan(package, RewardTier.Pass, session, catalog);

            Assert.IsFalse(planned.Succeeded);
            Assert.AreEqual(RewardRejection.CapacityBelowUsage, planned.Rejection);
            Assert.AreEqual(4, session.Economy.Dependencies.Capacity);
        }

        [Test]
        public void Apply_CreditsEveryTokenComponentAsItsOwnEntry()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);
            RewardPlan plan = RewardResolver.Plan(Package(), RewardTier.Benchmark, session, catalog).Plan;

            RewardResolution resolution = RewardResolver.Apply(plan, session, catalog);

            Assert.AreEqual(new TokenAmount(8), session.Economy.Tokens.Balance);
            Assert.AreEqual(3, session.Economy.Tokens.Entries.Count);
            Assert.AreEqual(TokenBasis.RewardTokens, session.Economy.Tokens.Entries[0].Basis);
            Assert.AreEqual(TokenBasis.RouteBonus, session.Economy.Tokens.Entries[1].Basis);
            Assert.AreEqual(TokenBasis.BenchmarkBonus, session.Economy.Tokens.Entries[2].Basis);
            Assert.IsNotNull(resolution.Record);
        }

        [Test]
        public void Apply_CreditsEveryTokenBeforeAnyChoiceIsPending()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);
            RewardPlan plan = RewardResolver.Plan(Package(), RewardTier.Benchmark, session, catalog).Plan;

            RewardResolution resolution = RewardResolver.Apply(plan, session, catalog);

            Assert.AreEqual(new TokenAmount(8), session.Economy.Tokens.Balance);
            Assert.AreEqual(1, resolution.Pending.Count);
        }

        [Test]
        public void Apply_AcquiresGuaranteedContentWithRewardOrigin()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);
            RewardPackageDefinition package = PackageOf(
                Component(RewardTier.Pass, RewardComponentKind.GuaranteedContent, null, ProgressionFixtures.ValuePlusThree));
            RewardPlan plan = RewardResolver.Plan(package, RewardTier.Pass, session, catalog).Plan;

            RewardResolver.Apply(plan, session, catalog);

            RepositoryEntry acquired = session.Repository.Entries[session.Repository.Entries.Count - 1];
            Assert.AreEqual(ProgressionFixtures.ValuePlusThree, acquired.Item.DefinitionID);
            Assert.AreEqual(AcquisitionOrigin.Reward, acquired.Origin);
        }

        [Test]
        public void Apply_ARAMSet_RaisesCapacity()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);
            RewardPackageDefinition package = PackageOf(
                Component(RewardTier.Pass, RewardComponentKind.RAMSet, 5, null));
            RewardPlan plan = RewardResolver.Plan(package, RewardTier.Pass, session, catalog).Plan;

            RewardResolver.Apply(plan, session, catalog);

            Assert.AreEqual(5, session.Economy.Dependencies.Capacity);
        }

        [Test]
        public void Apply_APatchComponent_CreatesAGrantThatHoldsTheResolutionOpen()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);
            RewardPackageDefinition package = PackageOf(
                Component(RewardTier.Pass, RewardComponentKind.Patch, null, "WB-PAT-001"));
            RewardPlan plan = RewardResolver.Plan(package, RewardTier.Pass, session, catalog).Plan;

            RewardResolution resolution = RewardResolver.Apply(plan, session, catalog);

            Assert.AreEqual(1, session.Economy.PatchGrants.Count);
            Assert.IsFalse(session.Economy.PatchGrants[0].Cached);
            Assert.IsFalse(resolution.IsComplete);
        }

        [Test]
        public void Apply_ACachedPatchComponent_DoesNotHoldTheResolutionOpen()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);
            RewardPackageDefinition package = PackageOf(
                Component(RewardTier.Pass, RewardComponentKind.CachedPatch, null, "WB-PAT-001"));
            RewardPlan plan = RewardResolver.Plan(package, RewardTier.Pass, session, catalog).Plan;

            RewardResolution resolution = RewardResolver.Apply(plan, session, catalog);

            Assert.AreEqual(1, session.Economy.PatchGrants.Count);
            Assert.IsTrue(session.Economy.PatchGrants[0].Cached);
            Assert.IsTrue(resolution.IsComplete);
        }

        [Test]
        public void IsComplete_TurnsTrueOnceTheGrantIsAttached()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);
            RewardPackageDefinition package = PackageOf(
                Component(RewardTier.Pass, RewardComponentKind.Patch, null, "WB-PAT-001"));
            RewardResolution resolution = RewardResolver.Apply(
                RewardResolver.Plan(package, RewardTier.Pass, session, catalog).Plan,
                session,
                catalog);
            InstanceID grant = session.Economy.PatchGrants[0].GrantIdentity;

            new PatchInstaller(session).Attach(new PatchAttachmentRequest(
                EconomyFixtures.PatchWithRule("WB-PAT-001", "FIXED_NUMBER_ADDITION_HOSTS"),
                session.Repository.Entries[0].Item.InstanceID,
                1,
                0,
                grant,
                "reward"));

            Assert.IsTrue(resolution.IsComplete);
        }

        [Test]
        public void Choose_AnItemMember_AcquiresItWithRewardOrigin()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);
            RewardResolution resolution = ApplyPoolPackage(session, catalog, PoolID);

            RewardChoiceResult chosen = resolution.Choose(0, ProgressionFixtures.ValuePlusThree, new InstanceID[0]);

            Assert.IsTrue(chosen.Succeeded);
            Assert.AreEqual(0, resolution.Pending.Count);
            RepositoryEntry acquired = session.Repository.Entries[session.Repository.Entries.Count - 1];
            Assert.AreEqual(AcquisitionOrigin.Reward, acquired.Origin);
            Assert.IsTrue(resolution.IsComplete);
        }

        [Test]
        public void Choose_ANonMember_RejectsNotAPoolMember()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);
            RewardResolution resolution = ApplyPoolPackage(session, catalog, PoolID);
            int before = session.Repository.Entries.Count;

            RewardChoiceResult chosen = resolution.Choose(0, ProgressionFixtures.ScorePlusValue, new InstanceID[0]);

            Assert.AreEqual(RewardRejection.NotAPoolMember, chosen.Rejection);
            Assert.AreEqual(before, session.Repository.Entries.Count);
            Assert.AreEqual(1, resolution.Pending.Count);
        }

        [Test]
        public void Choose_Twice_RejectsComponentNotPending()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);
            RewardResolution resolution = ApplyPoolPackage(session, catalog, PoolID);
            resolution.Choose(0, ProgressionFixtures.ValuePlusThree, new InstanceID[0]);

            RewardChoiceResult second = resolution.Choose(0, ProgressionFixtures.ValuePlusTwo, new InstanceID[0]);

            Assert.AreEqual(RewardRejection.ComponentNotPending, second.Rejection);
        }

        [Test]
        public void Choose_ADependencyMember_InstallsItAtPriceZero()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);
            RewardResolution resolution = ApplyPoolPackage(session, catalog, DependencyPoolID);

            RewardChoiceResult chosen = resolution.Choose(0, EconomyFixtures.OneRAMDependency, new InstanceID[0]);

            Assert.IsTrue(chosen.Succeeded);
            Assert.AreEqual(1, session.Economy.Dependencies.Usage);
            Assert.AreEqual(2, session.Economy.Dependencies.AllInstalled.Count);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
        }

        [Test]
        public void Choose_ADependencyWithoutRAM_SurfacesTheInstallationRejectionAndStaysPending()
        {
            SessionState session = Session(out ContentCatalog catalog, 20);
            DependencyInstaller installer = new(session);
            installer.Install(Install(EconomyFixtures.TwoRAMDependency, 2, 8, "WB-DEP-301"));
            installer.Install(Install(EconomyFixtures.TwoRAMDependency, 2, 8, "WB-DEP-302"));
            RewardResolution resolution = ApplyPoolPackage(session, catalog, DependencyPoolID);

            RewardChoiceResult chosen = resolution.Choose(0, EconomyFixtures.OneRAMDependency, new InstanceID[0]);

            Assert.IsFalse(chosen.Succeeded);
            Assert.AreEqual(RewardRejection.InstallationRejected, chosen.Rejection);
            Assert.AreEqual(DependencyRejection.InsufficientRAM, chosen.InstallationRejection);
            Assert.AreEqual(1, resolution.Pending.Count);
        }

        [Test]
        public void PlanThenApply_AtNoTier_IsCompleteAndEmpty()
        {
            SessionState session = Session(out ContentCatalog catalog, 0);

            RewardResolution resolution = RewardResolver.Apply(
                RewardResolver.Plan(Package(), null, session, catalog).Plan,
                session,
                catalog);

            Assert.IsTrue(resolution.IsComplete);
            Assert.AreEqual(0, resolution.Pending.Count);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
        }

        private static RewardResolution ApplyPoolPackage(SessionState session, ContentCatalog catalog, string poolID)
        {
            RewardPackageDefinition package = PackageOf(
                Component(RewardTier.Pass, RewardComponentKind.PoolChoice, null, poolID));

            return RewardResolver.Apply(RewardResolver.Plan(package, RewardTier.Pass, session, catalog).Plan, session, catalog);
        }

        private static DependencyInstallationRequest Install(
            string id,
            int ram,
            int price,
            string distinctID
        )
        {
            return new DependencyInstallationRequest(
                EconomyFixtures.Dependency(distinctID, ram),
                price,
                DependencyOrigin.Purchase,
                new InstanceID[0],
                "setup");
        }

        private static RewardPackageDefinition Package()
        {
            return PackageOf(
                Component(RewardTier.Pass, RewardComponentKind.Tokens, 4, null),
                Component(RewardTier.Pass, RewardComponentKind.RouteTokenBonus, 2, null),
                Component(RewardTier.Optimize, RewardComponentKind.PoolChoice, null, PoolID),
                Component(RewardTier.Optimize, RewardComponentKind.CachedPatch, null, "WB-PAT-001"),
                Component(RewardTier.Benchmark, RewardComponentKind.Tokens, 2, null));
        }

        private static RewardPackageDefinition PackageOf(params RewardComponent[] components)
        {
            return new RewardPackageDefinition(new RewardPackageID(PackageID), PackageID, components);
        }

        private static RewardComponent Component(
            RewardTier tier,
            RewardComponentKind kind,
            int? amount,
            string reference
        )
        {
            return new RewardComponent(tier, kind, amount, reference);
        }

        private static SessionState Session(out ContentCatalog catalog, int tokens)
        {
            catalog = EconomyFixtures.RewardCatalog(new[]
            {
                EconomyFixtures.Pool(PoolID, 0, ProgressionFixtures.ValuePlusTwo, ProgressionFixtures.ValuePlusThree),
                EconomyFixtures.Pool(DependencyPoolID, 0, EconomyFixtures.OneRAMDependency)
            });

            return EconomyFixtures.SessionOver(catalog, tokens);
        }
    }
}
