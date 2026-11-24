using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Application.Diagnostics;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// Plays the shipped onboarding twice: once through <see cref="ShippedCatalogSequenceDriver"/>,
    /// which calls the Domain directly, and once through <see cref="SessionFlow"/>. Both use the same
    /// archetype, identities, seed and player script, and must agree exactly. Every execution must
    /// produce an equal record whose written document is byte-identical, and at every Process entry
    /// the two Sessions must save byte-identical checkpoint documents.
    ///
    /// A difference here is a defect in Session Flow or a real divergence from the driver. It is
    /// surfaced, never adjusted away. The script covers Tutorial 1, its rewards, the first shop, and
    /// entry into Tutorial 2. That is as far as byte-identity can reach: an execution's identity
    /// counts within its Process, while the driver counts across its whole lifetime, so from Tutorial
    /// 2's first execution the two identities differ by design.
    /// </summary>
    public sealed class SessionFlowAgreementTests
    {
        private const string SystemIdentity = "WB-SYS-001";
        private const string SessionIdentity = "session-1";
        private const string SeedIdentity = "seed-1";
        private static readonly ProcessID _tutorialOne = new("WB-PROC-001");
        private static readonly ProcessID _tutorialTwo = new("WB-PROC-002");
        private static readonly ShopID _firstShop = new("WB-SHOP-001");

        private ContentCatalog _catalog;
        private ShippedCatalogSequenceDriver _driver;
        private AgreementSessionLifecycle _lifecycle;
        private AgreementScopeFactory _scopes;
        private SessionFlow _flow;

        [SetUp]
        public void SetUp()
        {
            _catalog = Catalog();
            StarterArchetypeDefinition archetype = _catalog.StarterArchetypes[0];
            _driver = new ShippedCatalogSequenceDriver(_catalog, archetype, SystemIdentity, SessionIdentity, SeedIdentity);

            CatalogHolder holder = new();
            holder.MarkLoaded(_catalog);
            _lifecycle = new AgreementSessionLifecycle();
            _scopes = new AgreementScopeFactory();
            _flow = new SessionFlow(
                holder,
                _lifecycle,
                _scopes,
                _scopes,
                Array.Empty<ICommandGate>(),
                Array.Empty<ICommandObserver>()
            );
        }

        [Test]
        public void TutorialOneThroughTheFirstShop_AgreesWithTheDriverAtEveryExecutionAndEveryEntry()
        {
            string driverEntry = AgreementSessionLifecycle.Document(_driver.Session, _tutorialOne, null);
            Assert.IsTrue(_flow.BeginSession(_catalog.StarterArchetypes[0], new SystemID(SystemIdentity), SessionIdentity, SeedIdentity).Succeeded);
            Assert.IsTrue(_driver.CreateProcess(Configuration(_tutorialOne)).Succeeded);
            AssertSameDocument(driverEntry, 0, "entry into Tutorial 1");

            ExecutionOutcome first = ExecuteBoth(1);
            InstanceID arrived = ReturnBoth(first, 1);
            InstallBoth(arrived, 5);
            ExecutionOutcome second = ExecuteBoth(2);
            InstanceID next = ReturnBoth(second, 2);
            InstallBoth(next, 6);
            ExecutionOutcome final = ExecuteBoth(3);

            Assert.IsTrue(final.Evaluation.Reached.HasValue, "The script must pass Tutorial 1 for the Session to continue.");
            RewardResolution driverRewards = _driver.ResolveRewards(final.Record);
            ResolveRewardsBoth(driverRewards);
            _driver.CompleteProcess();
            Assert.IsTrue(_driver.OpenShop(_firstShop, _tutorialTwo).Succeeded);
            Assert.AreEqual(SessionStage.Shop, Stage());

            string driverSecondEntry = AgreementSessionLifecycle.Document(_driver.Session, _tutorialTwo, null);
            Assert.IsTrue(_flow.Execute(new LeaveShop()).Accepted);
            Assert.IsTrue(_driver.CreateProcess(Configuration(_tutorialTwo)).Succeeded);
            AssertSameDocument(driverSecondEntry, 1, "entry into Tutorial 2");
            Assert.AreEqual(2, _lifecycle.EntryDocuments.Count);
        }

        /// <summary>
        /// Executes the current source on both sides and asserts the records agree.
        /// </summary>
        /// <param name="ordinal">The execution's one-based ordinal, for messages.</param>
        /// <returns>The Session Flow side's outcome.</returns>
        private ExecutionOutcome ExecuteBoth(int ordinal)
        {
            CompilationAttempt attempt = _driver.Compile();
            Assert.IsTrue(attempt.Committed, $"The driver's compilation {ordinal} must commit.");
            ExecutionRecord expected = _driver.Execute(attempt.Source);

            CommandResult result = _flow.Execute(new CompileAndExecute());
            Assert.IsTrue(result.Accepted, $"Session Flow's execution {ordinal} was refused: {result.DomainResult}.");
            ExecutionOutcome outcome = (ExecutionOutcome)result.DomainResult;

            Assert.AreEqual(expected.Header.ExecutionIdentity, outcome.Record.Header.ExecutionIdentity, $"execution {ordinal} identity");
            Assert.AreEqual(ExecutionRecordDocument.Write(expected), ExecutionRecordDocument.Write(outcome.Record), $"execution {ordinal} document");
            Assert.IsTrue(expected.Equals(outcome.Record), $"execution {ordinal} record equality");
            return outcome;
        }

        /// <summary>
        /// Leaves Result Review on the Session Flow side by the exit its result allows, and fires the
        /// same arrival on the driver, asserting both admit the same item.
        /// </summary>
        /// <param name="outcome">The execution just run.</param>
        /// <param name="afterExecution">The execution the arrival follows.</param>
        /// <returns>The arrived item's identity.</returns>
        private InstanceID ReturnBoth(ExecutionOutcome outcome, int afterExecution)
        {
            ISessionCommand exit = outcome.Evaluation.Reached.HasValue ? new Continue() : new ReturnToBuild();
            CommandResult result = _flow.Execute(exit);
            Assert.IsTrue(result.Accepted, $"Leaving Result Review after execution {afterExecution} was refused.");
            ArrivalResult flowArrival = (ArrivalResult)result.DomainResult;
            ArrivalResult driverArrival = _driver.Arrive(afterExecution);

            Assert.AreEqual(driverArrival.Succeeded, flowArrival.Succeeded);
            Assert.AreEqual(driverArrival.Arrived.Count, flowArrival.Arrived.Count);
            Assert.IsTrue(driverArrival.Arrived.Count > 0, $"The script needs an arrival after execution {afterExecution}.");
            Assert.AreEqual(driverArrival.Arrived[0].InstanceID, flowArrival.Arrived[0].InstanceID);
            return driverArrival.Arrived[0].InstanceID;
        }

        /// <summary>
        /// Installs an item at a line on both sides.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="line">The one-based line.</param>
        private void InstallBoth(InstanceID item, int line)
        {
            Assert.IsTrue(_driver.Install(item, line).Succeeded, $"The driver could not install {item} at line {line}.");
            Assert.IsTrue(_flow.Execute(new InstallLine(item, new SourcePosition(line))).Accepted, $"Session Flow could not install {item} at line {line}.");
        }

        /// <summary>
        /// Resolves every pending reward choice the same way on both sides: the first option of each.
        /// </summary>
        /// <param name="driverRewards">The driver's resolution.</param>
        private void ResolveRewardsBoth(RewardResolution driverRewards)
        {
            while (driverRewards.Pending.Count > 0)
            {
                PendingReward pending = driverRewards.Pending[0];
                string choice = pending.Pool.Members[0].Content;
                Assert.IsTrue(driverRewards.Choose(pending.ComponentIndex, choice, Array.Empty<InstanceID>()).Succeeded);
                Assert.IsTrue(_flow.Execute(new SelectReward(pending.ComponentIndex, choice, Array.Empty<InstanceID>())).Accepted);
            }
        }

        /// <summary>
        /// Asserts the Session Flow side's checkpoint document at an entry equals the driver's.
        /// </summary>
        /// <param name="expected">The driver's document.</param>
        /// <param name="entry">The entry's index.</param>
        /// <param name="moment">The entry, for messages.</param>
        private void AssertSameDocument(string expected, int entry, string moment)
        {
            Assert.Greater(_lifecycle.EntryDocuments.Count, entry, $"Session Flow announced no {moment}.");
            Assert.AreEqual(expected, _lifecycle.EntryDocuments[entry], $"checkpoint document at {moment}");
        }

        /// <summary>
        /// The stage the Session Flow side stands in.
        /// </summary>
        /// <returns>The stage.</returns>
        private SessionStage Stage()
        {
            return new SessionQueryService(_flow).Stage;
        }

        /// <summary>
        /// A shipped Process configuration.
        /// </summary>
        /// <param name="process">The Process.</param>
        /// <returns>The configuration.</returns>
        private ProcessConfigurationDefinition Configuration(ProcessID process)
        {
            Assert.IsTrue(_catalog.TryGetProcessConfiguration(process, out ProcessConfigurationDefinition configuration));
            return configuration;
        }

        /// <summary>
        /// Loads the shipped catalog.
        /// </summary>
        /// <returns>The frozen catalog.</returns>
        private static ContentCatalog Catalog()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Catalog");
            CatalogDirectorySource source = new(root);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            return loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
