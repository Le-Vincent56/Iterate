using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests the eight views: each reads what the holders hold, and each is a copy, so a change made
    /// after the view was taken does not reach it. Views hold lists, so they are compared member by
    /// member, never as whole records. No view carries a decision record, a decision context or a
    /// seed, and the Process view counts pending arrivals without naming them.
    /// </summary>
    public sealed class ViewMapperTests
    {
        [Test]
        public void SessionView_ReadsTheEconomyAndPossessionsAndIsACopy()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);

            SessionView view = rig.Queries.Session();
            rig.Flow.Execute(new ArchiveItem(Item(rig, SessionFlowFixtures.Structure)));
            rig.Scopes.Session.Economy.Tokens.Credit(new TokenAmount(5), TokenBasis.RewardTokens, "test");

            Assert.AreEqual(SessionStage.Build, view.Stage);
            Assert.AreEqual(0, view.Tokens, "The credit after the view was taken does not reach it.");
            Assert.AreEqual(SessionFlowPlayContent.StarterItems().Length, view.Repository.Count);
            Assert.AreEqual(SessionFlowFixtures.Instruction, view.Repository[0].DefinitionID);
            Assert.AreEqual(1, view.Dependencies.Count);
            Assert.IsTrue(view.Dependencies[0].Starter);
            Assert.AreEqual(4, view.RAMCapacity);
            Assert.IsNull(view.CommittedRoute);
        }

        [Test]
        public void ProcessView_ReadsTheBufferAndCountersAndIsACopy()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            InstanceID structure = Item(rig, SessionFlowFixtures.Structure);

            ProcessView view = rig.Queries.Process();
            rig.Flow.Execute(new ArchiveItem(structure));

            Assert.AreEqual(SessionFlowPlayContent.PlayFirst.Value, view.ProcessIdentity);
            Assert.AreEqual(8, view.Slots.Count);
            Assert.AreEqual(SessionFlowPlayContent.StarterItems().Length, CountHeld(view.Slots));
            Assert.AreEqual(30, view.Bytes);
            Assert.AreEqual(0, view.ExecutionsRun);
            Assert.AreEqual(2, view.ExecutionsRemaining);
            Assert.AreEqual(0, view.PendingArrivals);
            CollectionAssert.IsEmpty(view.Archived, "The archive after the view was taken does not reach it.");
            CollectionAssert.Contains(rig.Queries.Process().Archived, structure);
        }

        [Test]
        public void BuildView_ReadsTheSourceAndStatusAndIsACopy()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            InstanceID item = Item(rig, SessionFlowFixtures.Instruction);

            BuildView view = rig.Queries.Build();
            rig.Flow.Execute(new InstallLine(item, new SourcePosition(2)));

            Assert.AreEqual(7, view.Slots.Count);
            Assert.AreEqual(SourceSlotKind.Core, view.Slots[0].Kind);
            Assert.AreEqual(SourceSlotKind.Empty, view.Slots[1].Kind, "The install after the view was taken does not reach it.");
            Assert.IsFalse(view.Status.SourceCompiled);
            SourceSlotView installed = rig.Queries.Build().Slots[1];
            Assert.AreEqual(item, installed.Item);
            Assert.AreEqual(SessionFlowFixtures.Instruction, installed.DefinitionID);
        }

        [Test]
        public void BranchView_ReadsTheDraftAndIsACopy()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.BranchFirstSystem);

            BranchView view = rig.Queries.Branch();
            rig.Flow.Execute(new ConfigureBranch(BranchAction.Exclude, Item(rig, SessionFlowFixtures.Instruction)));

            Assert.AreEqual(3, view.SelectedCount);
            Assert.AreEqual(2, view.Capacity);
            Assert.IsTrue(view.IsOverCapacity);
            Assert.IsFalse(view.IsValid);
            Assert.AreEqual(3, view.Items.Count);
            Assert.IsTrue(view.Items[2].State.HasFlag(BranchInstanceState.Required));
            Assert.AreEqual(2, rig.Queries.Branch().SelectedCount);
        }

        [Test]
        public void ShopView_ReadsTheSlotsAndIsACopy()
        {
            SessionFlowRig rig = PlayIntoTheShop();

            ShopView view = rig.Queries.Shop();
            rig.Flow.Execute(new PinOffer(1, true));

            Assert.AreEqual(SessionFlowPlayContent.PlayShop.Value, view.ShopIdentity);
            Assert.AreEqual(SessionFlowPlayContent.PlayLast, view.Target);
            Assert.AreEqual(4, view.Slots.Count);
            Assert.AreEqual(SessionFlowFixtures.OfferedInstruction, view.Slots[0].ContentID);
            Assert.AreEqual(ContentCategory.Instruction, view.Slots[0].Category);
            Assert.AreEqual(1, view.Slots[0].Price);
            Assert.IsTrue(view.ServicesEnabled);
            Assert.IsFalse(view.Slots[0].Pinned, "The pin after the view was taken does not reach it.");
            Assert.IsTrue(rig.Queries.Shop().Slots[0].Pinned);
        }

        [Test]
        public void ResultView_ReadsTheRecordAndEvaluation()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            rig.Flow.Execute(new CompileAndExecute());

            ResultView view = rig.Queries.Result();

            Assert.AreEqual("WB-PROC-101:execution:1", view.ExecutionIdentity);
            Assert.AreEqual(1, view.FinalOutput);
            Assert.IsTrue(view.Valid);
            Assert.IsNull(view.Reached);
            Assert.AreEqual(1, view.ExecutionsRemaining);
        }

        [Test]
        public void RewardView_ReadsThePendingChoiceAndIsACopy()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.ChoiceSystem);
            RewardCommandTests.PassAndSubmit(rig, SessionFlowPlayContent.OverwriteInstruction);

            RewardView view = rig.Queries.Rewards();
            rig.Flow.Execute(new SelectReward(view.Pending[0].ComponentIndex, SessionFlowFixtures.OfferedInstruction, Array.Empty<InstanceID>()));

            Assert.IsFalse(view.IsComplete);
            Assert.AreEqual(1, view.Pending.Count);
            Assert.AreEqual(RewardComponentKind.PoolChoice, view.Pending[0].Kind);
            CollectionAssert.AreEqual(new[] { SessionFlowFixtures.OfferedInstruction, SessionFlowFixtures.Structure }, view.Pending[0].Options);
            CollectionAssert.IsEmpty(view.OpenGrants);
            Assert.IsNull(rig.Queries.Rewards(), "The Session ended with the choice; the earlier view still holds what it copied.");
        }

        [Test]
        public void RewardView_ListsAnUnattachedGrant()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PatchSystem);
            RewardCommandTests.PassAndSubmit(rig, SessionFlowFixtures.Instruction);

            RewardView view = rig.Queries.Rewards();

            Assert.AreEqual(1, view.OpenGrants.Count);
            Assert.AreEqual(rig.Scopes.Session.Economy.PatchGrants[0].GrantIdentity, view.OpenGrants[0]);
        }

        [Test]
        public void RouteView_ReadsTheOfferedRoutes()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.RouteFirstSystem);

            RouteView view = rig.Queries.Routes();

            Assert.AreEqual(2, view.Options.Count);
            Assert.AreEqual(SessionFlowFixtures.RouteOne, view.Options[0].Route);
            Assert.AreEqual(SessionFlowFixtures.RouteOneProcess, view.Options[0].Process);
            Assert.AreEqual(SessionFlowFixtures.RouteOneShop, view.Options[0].Shop);
            Assert.IsNull(view.Committed);
        }

        [Test]
        public void Views_CarryNoDecisionRecordNoDecisionContextAndNoSeed()
        {
            Type[] views =
            {
                typeof(SessionView), typeof(ProcessView), typeof(BuildView), typeof(BranchView),
                typeof(ShopView), typeof(ResultView), typeof(RewardView), typeof(RouteView),
            };
            List<string> offenders = new();
            HashSet<Type> visited = new();
            foreach (Type view in views)
                Sweep(view, view.Name, visited, offenders);

            CollectionAssert.IsEmpty(offenders, string.Join("; ", offenders));
        }

        [Test]
        public void ProcessView_CountsPendingArrivalsWithoutNamingThem()
        {
            PropertyInfo pending = typeof(ProcessView).GetProperty(nameof(ProcessView.PendingArrivals));

            Assert.AreEqual(typeof(int), pending.PropertyType);
            foreach (PropertyInfo property in typeof(ProcessView).GetProperties())
                StringAssert.DoesNotContain("Pending", property.Name == nameof(ProcessView.PendingArrivals) ? string.Empty : property.Name);
        }

        /// <summary>
        /// Walks a type's public properties, and the types they hold, recording any decision record,
        /// decision context or seed it finds.
        /// </summary>
        /// <param name="type">The type to walk.</param>
        /// <param name="path">The member path that reached it.</param>
        /// <param name="visited">The types already walked.</param>
        /// <param name="offenders">The findings.</param>
        private static void Sweep(
            Type type,
            string path,
            HashSet<Type> visited,
            List<string> offenders
        )
        {
            if (type == typeof(RandomDecisionRecord) || type == typeof(DecisionContext))
            {
                offenders.Add(path);
                return;
            }

            if (type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                    Sweep(argument, path, visited, offenders);
            }

            if (type.Namespace == null || !type.Namespace.StartsWith("Iterate", StringComparison.Ordinal) || !visited.Add(type))
                return;

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name.IndexOf("Seed", StringComparison.OrdinalIgnoreCase) >= 0)
                    offenders.Add($"{path}.{property.Name}");

                Sweep(property.PropertyType, $"{path}.{property.Name}", visited, offenders);
            }
        }

        /// <summary>
        /// Plays the first play Process at Pass and stands in the shop after it.
        /// </summary>
        /// <returns>The rig in the shop.</returns>
        private static SessionFlowRig PlayIntoTheShop()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            RewardCommandTests.PassAndSubmit(rig, SessionFlowPlayContent.OverwriteInstruction);
            Assert.AreEqual(SessionStage.Shop, rig.Queries.Stage);
            return rig;
        }

        /// <summary>
        /// The first Repository entry of a definition.
        /// </summary>
        /// <param name="rig">The rig.</param>
        /// <param name="definition">The content ID.</param>
        /// <returns>The instance identity.</returns>
        private static InstanceID Item(SessionFlowRig rig, string definition)
        {
            return rig.Scopes.Session.Repository.EntriesOf(definition)[0].Item.InstanceID;
        }

        /// <summary>
        /// Counts the Buffer slot views holding an item.
        /// </summary>
        /// <param name="slots">The slot views.</param>
        /// <returns>The count.</returns>
        private static int CountHeld(IReadOnlyList<BufferSlotView> slots)
        {
            int count = 0;
            foreach (BufferSlotView slot in slots)
            {
                if (slot.Item.HasValue)
                    count++;
            }

            return count;
        }
    }
}
