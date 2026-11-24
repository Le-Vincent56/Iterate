using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests <see cref="StagePlan"/> over the vertical-slice-shaped fixture System: the full walk with
    /// either route, route commitment and its refusals, the Process a shop targets, and rewinding for
    /// recovery and restore. Steps are compared member by member, because a step holds a list.
    /// </summary>
    public sealed class StagePlanTests
    {
        private ContentCatalog _catalog;
        private SystemDefinition _system;

        [SetUp]
        public void SetUp()
        {
            _catalog = SessionFlowFixtures.Catalog();
            Assert.IsTrue(_catalog.TryGetSystem(SessionFlowFixtures.MainSystem, out _system));
        }

        [Test]
        public void Walk_WithRouteOne_YieldsTheEightStepsThenEnd()
        {
            StagePlan plan = new(_system, _catalog);

            List<string> walked = Walk(plan, SessionFlowFixtures.RouteOne);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Process WB-PROC-001",
                    "Shop WB-SHOP-001",
                    "Process WB-PROC-002",
                    "RouteSelection",
                    "Shop WB-SHOP-002",
                    "Process WB-PROC-003",
                    "Shop WB-SHOP-004",
                    "Process WB-PROC-005",
                    "End",
                },
                walked
            );
        }

        [Test]
        public void Walk_WithRouteTwo_YieldsTheEightStepsThenEnd()
        {
            StagePlan plan = new(_system, _catalog);

            List<string> walked = Walk(plan, SessionFlowFixtures.RouteTwo);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Process WB-PROC-001",
                    "Shop WB-SHOP-001",
                    "Process WB-PROC-002",
                    "RouteSelection",
                    "Shop WB-SHOP-003",
                    "Process WB-PROC-004",
                    "Shop WB-SHOP-004",
                    "Process WB-PROC-005",
                    "End",
                },
                walked
            );
        }

        [Test]
        public void Advance_PastEnd_StaysOnEnd()
        {
            StagePlan plan = new(_system, _catalog);
            Walk(plan, SessionFlowFixtures.RouteOne);

            Assert.AreEqual(PlannedStepKind.End, plan.Advance().Kind);
            Assert.AreEqual(PlannedStepKind.End, plan.Current.Kind);
        }

        [Test]
        public void RouteSelectionStep_OffersBothRoutesInAuthoredOrder()
        {
            StagePlan plan = new(_system, _catalog);
            AdvanceTo(plan, PlannedStepKind.RouteSelection);

            CollectionAssert.AreEqual(new[] { SessionFlowFixtures.RouteOne, SessionFlowFixtures.RouteTwo }, plan.Current.Routes);
            Assert.IsNull(plan.Current.Process);
            Assert.IsNull(plan.Current.Shop);
        }

        [Test]
        public void TryCommitRoute_OffARouteStep_IsRefusedAndChangesNothing()
        {
            StagePlan plan = new(_system, _catalog);

            Assert.IsFalse(plan.TryCommitRoute(SessionFlowFixtures.RouteOne));
            Assert.IsNull(plan.CommittedRoute);
            Assert.AreEqual(PlannedStepKind.Process, plan.Current.Kind);
            Assert.AreEqual(SessionFlowFixtures.TutorialOne, plan.Current.Process);
        }

        [Test]
        public void TryCommitRoute_ARouteTheChoiceDoesNotOffer_IsRefusedAndChangesNothing()
        {
            StagePlan plan = new(_system, _catalog);
            AdvanceTo(plan, PlannedStepKind.RouteSelection);

            Assert.IsFalse(plan.TryCommitRoute(new RouteID("WB-ROUTE-999")));
            Assert.IsNull(plan.CommittedRoute);
            Assert.AreEqual(PlannedStepKind.Shop, plan.Advance().Kind);
            Assert.AreEqual(SessionFlowFixtures.LastShop, plan.Current.Shop, "With nothing committed the walk resumes at the next authored stage.");
        }

        [Test]
        public void TryCommitRoute_ARouteTheCatalogDoesNotDefine_IsRefusedAndChangesNothing()
        {
            SystemDefinition system = new(
                new SystemID("WB-SYS-UNDEFINED-ROUTE"),
                "Undefined route",
                new[] { new SystemStage(SystemStageKind.RouteSelection, null, null, new[] { new RouteID("WB-ROUTE-777") }) }
            );
            StagePlan plan = new(system, _catalog);

            Assert.IsFalse(plan.TryCommitRoute(new RouteID("WB-ROUTE-777")));
            Assert.IsNull(plan.CommittedRoute);
            Assert.AreEqual(PlannedStepKind.End, plan.Advance().Kind);
        }

        [Test]
        public void TryCommitRoute_ASecondCommit_IsRefusedAndKeepsTheFirst()
        {
            StagePlan plan = new(_system, _catalog);
            AdvanceTo(plan, PlannedStepKind.RouteSelection);
            Assert.IsTrue(plan.TryCommitRoute(SessionFlowFixtures.RouteOne));

            Assert.IsFalse(plan.TryCommitRoute(SessionFlowFixtures.RouteTwo));
            Assert.AreEqual(SessionFlowFixtures.RouteOne, plan.CommittedRoute);
            Assert.AreEqual(SessionFlowFixtures.RouteOneShop, plan.Advance().Shop);
        }

        [Test]
        public void NextProcessAfterCurrent_AtTheFirstShop_IsTheSecondTutorial()
        {
            StagePlan plan = new(_system, _catalog);
            plan.Advance();

            Assert.AreEqual(SessionFlowFixtures.FirstShop, plan.Current.Shop);
            Assert.AreEqual(SessionFlowFixtures.TutorialTwo, plan.NextProcessAfterCurrent());
        }

        [Test]
        public void NextProcessAfterCurrent_AtTheRouteShop_IsTheRouteProcess()
        {
            StagePlan plan = new(_system, _catalog);
            AdvanceTo(plan, PlannedStepKind.RouteSelection);
            plan.TryCommitRoute(SessionFlowFixtures.RouteTwo);
            plan.Advance();

            Assert.AreEqual(SessionFlowFixtures.RouteTwoShop, plan.Current.Shop);
            Assert.AreEqual(SessionFlowFixtures.RouteTwoProcess, plan.NextProcessAfterCurrent());
        }

        [Test]
        public void NextProcessAfterCurrent_AtTheLastShop_IsTheCriticalProcess()
        {
            StagePlan plan = new(_system, _catalog);
            AdvanceTo(plan, PlannedStepKind.RouteSelection);
            plan.TryCommitRoute(SessionFlowFixtures.RouteOne);
            plan.Advance();
            plan.Advance();
            plan.Advance();

            Assert.AreEqual(SessionFlowFixtures.LastShop, plan.Current.Shop);
            Assert.AreEqual(SessionFlowFixtures.CriticalProcess, plan.NextProcessAfterCurrent());
        }

        [Test]
        public void NextProcessAfterCurrent_BeforeAnUncommittedRouteChoice_IsNull()
        {
            SystemDefinition system = new(
                new SystemID("WB-SYS-SHOP-THEN-ROUTE"),
                "Shop then route",
                new[] { SessionFlowFixtures.ShopStage(SessionFlowFixtures.FirstShop), SessionFlowFixtures.RouteStage() }
            );
            StagePlan plan = new(system, _catalog);

            Assert.IsNull(plan.NextProcessAfterCurrent());
        }

        [Test]
        public void TryRewindTo_AVisitedAuthoredProcess_StandsOnItAndResumesTheWalkFromThere()
        {
            StagePlan plan = new(_system, _catalog);
            AdvanceTo(plan, PlannedStepKind.RouteSelection);

            Assert.IsTrue(plan.TryRewindTo(SessionFlowFixtures.TutorialTwo));
            Assert.AreEqual(PlannedStepKind.Process, plan.Current.Kind);
            Assert.AreEqual(SessionFlowFixtures.TutorialTwo, plan.Current.Process);
            Assert.AreEqual(PlannedStepKind.RouteSelection, plan.Advance().Kind);
        }

        [Test]
        public void TryRewindTo_TheCommittedRouteProcess_StandsOnItAndResumesAfterTheChoice()
        {
            StagePlan plan = new(_system, _catalog);
            AdvanceTo(plan, PlannedStepKind.RouteSelection);
            plan.TryCommitRoute(SessionFlowFixtures.RouteOne);
            plan.Advance();
            plan.Advance();
            plan.Advance();

            Assert.IsTrue(plan.TryRewindTo(SessionFlowFixtures.RouteOneProcess));
            Assert.AreEqual(SessionFlowFixtures.RouteOneProcess, plan.Current.Process);
            Assert.AreEqual(SessionFlowFixtures.RouteOne, plan.CommittedRoute);
            Assert.AreEqual(SessionFlowFixtures.LastShop, plan.Advance().Shop);
        }

        [Test]
        public void TryRewindTo_ARouteProcessWithNoRouteCommitted_IsRefusedAndChangesNothing()
        {
            StagePlan plan = new(_system, _catalog);

            Assert.IsFalse(plan.TryRewindTo(SessionFlowFixtures.RouteOneProcess));
            Assert.AreEqual(SessionFlowFixtures.TutorialOne, plan.Current.Process);
        }

        [Test]
        public void TryRewindTo_AnUnknownProcess_IsRefusedAndChangesNothing()
        {
            StagePlan plan = new(_system, _catalog);
            plan.Advance();

            Assert.IsFalse(plan.TryRewindTo(new ProcessID("WB-PROC-999")));
            Assert.AreEqual(SessionFlowFixtures.FirstShop, plan.Current.Shop);
        }

        [Test]
        public void Constructor_NullSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new StagePlan(null, _catalog));
        }

        [Test]
        public void Constructor_NullCatalog_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new StagePlan(_system, null));
        }

        /// <summary>
        /// Walks a plan to its end, committing a route at the choice, and describes each step.
        /// </summary>
        /// <param name="plan">The plan.</param>
        /// <param name="route">The route to commit.</param>
        /// <returns>The described steps, the end step included.</returns>
        private static List<string> Walk(StagePlan plan, RouteID route)
        {
            List<string> walked = new() { Describe(plan.Current) };
            while (plan.Current.Kind != PlannedStepKind.End)
            {
                if (plan.Current.Kind == PlannedStepKind.RouteSelection)
                    Assert.IsTrue(plan.TryCommitRoute(route));

                walked.Add(Describe(plan.Advance()));
                Assert.Less(walked.Count, 20, "The walk did not terminate.");
            }

            return walked;
        }

        /// <summary>
        /// Advances a plan until it stands on a step of a kind.
        /// </summary>
        /// <param name="plan">The plan.</param>
        /// <param name="kind">The kind to stop at.</param>
        private static void AdvanceTo(StagePlan plan, PlannedStepKind kind)
        {
            while (plan.Current.Kind != kind)
            {
                Assert.AreNotEqual(PlannedStepKind.End, plan.Current.Kind, $"The plan holds no {kind} step.");
                plan.Advance();
            }
        }

        /// <summary>
        /// Describes a step by its kind and reference.
        /// </summary>
        /// <param name="step">The step.</param>
        /// <returns>The description.</returns>
        private static string Describe(PlannedStep step)
        {
            switch (step.Kind)
            {
                case PlannedStepKind.Process:
                    return $"Process {step.Process.Value.Value}";

                case PlannedStepKind.Shop:
                    return $"Shop {step.Shop.Value.Value}";

                default:
                    return step.Kind.ToString();
            }
        }
    }
}
