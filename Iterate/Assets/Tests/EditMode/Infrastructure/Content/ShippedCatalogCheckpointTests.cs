using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Application.Logging;
using Iterate.Application.Persistence;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;
using Iterate.Infrastructure.Logging;
using Iterate.Infrastructure.Persistence;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// The child's end-to-end evidence: the shipped onboarding driven to a Process entry, captured,
    /// written through the real file adapter into a temporary directory, read back, restored against the
    /// real frozen catalog, and played on from there — with every assertion made against a second driver
    /// that was never interrupted, so the only difference between the two is the save.
    ///
    /// Every capture is taken after the Branch is confirmed and before the Process is created. That is a
    /// contract rather than a convenience: creating a Process with a Process rule allocates an identity
    /// from the Session's source, so a capture taken afterwards would leave the restored side one behind
    /// for the rest of the Session. The rule-identity and next-allocation assertions are what would
    /// catch it.
    ///
    /// The authored flow reaches POWER SURGE through the WB-SHOP-003 route shop, which these tests skip
    /// exactly as <c>ConfirmedPowerSurgeBranch</c> does. Session-scoped shop ordinals therefore start at
    /// one on both sides, which is why the shop case can compare reroll decisions directly.
    /// </summary>
    public sealed class ShippedCatalogCheckpointTests
    {
        private const string TutorialOne = "WB-PROC-001";

        private const string PowerSurge = "WB-PROC-004";

        private const string ThermalThrottle = "WB-PROC-005";

        private const string PreCriticalShop = "WB-SHOP-004";

        private const string PowerSurgeRoute = "WB-ROUTE-002";

        private const string ConditionValueEven = "WB-STR-002";

        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "IterateCheckpoint_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void Capture_AtPowerSurgeEntry_MatchesTheSession()
        {
            ShippedCatalogSequenceDriver driver = ConfirmedPowerSurgeBranch();

            CheckpointState state = Capture(driver);

            Assert.AreEqual(Catalog().Revision, state.CatalogRevision);
            Assert.AreEqual(new ProcessID(PowerSurge), state.Process);
            Assert.AreEqual(new RouteID(PowerSurgeRoute), state.Route);
            Assert.AreEqual(driver.Session.Repository.Entries.Count, state.Repository.Count);
            Assert.AreEqual(driver.Session.StarterDependency.InstanceID, state.Economy.Starter.Instance);
            Assert.AreEqual(driver.Session.InstanceIDs.LastAllocated, state.LastAllocatedInstance);
            CollectionAssert.AreEqual(driver.Session.ActiveBranch.Confirmed.Eligible, state.Branch);
        }

        [Test]
        public void SaveThenLoad_ThroughTheRealAdapter_RoundTripsTheCheckpoint()
        {
            CheckpointState state = Capture(ConfirmedPowerSurgeBranch());
            PersistenceService service = Service();

            SaveResult saved = service.SaveCheckpointAsync(state, CancellationToken.None).GetAwaiter().GetResult();
            LoadResult<CheckpointState> loaded = service.LoadCheckpointAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(SaveOutcome.Saved, saved.Outcome);
            Assert.AreEqual(LoadOutcome.Loaded, loaded.Outcome, loaded.Defect);

            // Compared as the document each state encodes to, not with Assert.AreEqual: CheckpointState
            // is a record over four lists, so its generated equality is reference equality on them and a
            // round trip rebuilds every one. Re-encoding is the stronger claim in any case — it says the
            // bytes that came off disk describe the same checkpoint in every member persistence carries.
            Assert.AreEqual(Encoded(state), Encoded(loaded.Value));
            Assert.AreEqual(state.Process, loaded.Value.Process);
            Assert.AreEqual(state.Route, loaded.Value.Route);
            Assert.AreEqual(state.LastAllocatedInstance, loaded.Value.LastAllocatedInstance);
            CollectionAssert.AreEqual(state.Branch, loaded.Value.Branch);
        }

        [Test]
        public void TheSavedFile_IsAVersionedDocumentOnDisk()
        {
            Service().SaveCheckpointAsync(Capture(ConfirmedPowerSurgeBranch()), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            string text = File.ReadAllText(Path.Combine(_root, SaveFiles.Checkpoint));

            StringAssert.StartsWith("{\n  \"formatVersion\": 1", text);
        }

        [Test]
        public void Restore_DerivesTheRecoveryFromTheAuthoredRole(
            [Values(PowerSurge, ThermalThrottle)] string routeProcess
        )
        {
            // WB-PROC-004 is RouteProcess3 and WB-PROC-005 is CriticalProcess: different roles, same
            // recovery, and the destination is keyed on the role rather than on anything that merely
            // correlates with it.
            CheckpointState state = Capture(ConfirmedPowerSurgeBranch()) with { Process = new ProcessID(routeProcess) };

            CheckpointRestoreResult result = CheckpointRestore.Restore(state, Catalog());

            Assert.IsTrue(result.Succeeded, result.Detail);
            Assert.AreEqual(CheckpointRecovery.BranchConfiguration, result.Destination.Recovery);
        }

        [Test]
        public void Restore_OfATutorialProcess_RecoversByScriptedRestart()
        {
            CheckpointState state = Capture(ConfirmedPowerSurgeBranch()) with
            {
                Process = new ProcessID(TutorialOne),
                Route = null
            };

            CheckpointRestoreResult result = CheckpointRestore.Restore(state, Catalog());

            Assert.IsTrue(result.Succeeded, result.Detail);
            Assert.AreEqual(CheckpointRecovery.ScriptedRestart, result.Destination.Recovery);
        }

        [Test]
        public void Restore_WithAnUnchangedBranch_ReproducesTheArrivals()
        {
            ShippedCatalogSequenceDriver carried = ConfirmedPowerSurgeBranch();
            ShippedCatalogSequenceDriver restored = RestoredFrom(carried);
            ProcessConfigurationDefinition configuration = Configuration(PowerSurge);

            Assert.IsTrue(carried.CreateProcess(configuration).Succeeded);
            Assert.IsTrue(restored.CreateProcess(configuration).Succeeded);

            CollectionAssert.AreEqual(SelectedIdentities(carried.Process), SelectedIdentities(restored.Process));
        }

        [Test]
        public void Restore_WithAModifiedBranch_ReusesThePositions()
        {
            // A retry may legally change the Branch, and the draw positions restart at one per Process,
            // so the modified side consumes the same positions over a different composition.
            ShippedCatalogSequenceDriver carried = ConfirmedPowerSurgeBranch();
            ShippedCatalogSequenceDriver restored = RestoredFrom(carried);
            ProcessConfigurationDefinition configuration = Configuration(PowerSurge);
            Reconfirm(restored, configuration);

            Assert.IsTrue(carried.CreateProcess(configuration).Succeeded);
            Assert.IsTrue(restored.CreateProcess(configuration).Succeeded);

            CollectionAssert.AreEqual(Positions(carried.Process), Positions(restored.Process));
        }

        [Test]
        public void Restore_ThenOpeningTheCriticalShop_RerollsIdentically()
        {
            ShippedCatalogSequenceDriver carried = ConfirmedPowerSurgeBranch();
            ShippedCatalogSequenceDriver restored = RestoredFrom(carried);
            Credit(carried);
            Credit(restored);

            RerollResult carriedRoll = OpenAndReroll(carried);
            RerollResult restoredRoll = OpenAndReroll(restored);

            Assert.IsTrue(carriedRoll.Succeeded, carriedRoll.Rejection.ToString());
            Assert.IsTrue(restoredRoll.Succeeded, restoredRoll.Rejection.ToString());
            Assert.AreEqual(carriedRoll.Decision, restoredRoll.Decision);
            CollectionAssert.AreEqual(Contents(carried.Shop), Contents(restored.Shop));
        }

        [Test]
        public void Restore_ResetsTheProcessRatherThanResumingIt()
        {
            ShippedCatalogSequenceDriver carried = ConfirmedPowerSurgeBranch();
            ShippedCatalogSequenceDriver restored = RestoredFrom(carried);
            ProcessConfigurationDefinition configuration = Configuration(PowerSurge);

            Assert.IsTrue(restored.CreateProcess(configuration).Succeeded);

            Assert.AreEqual(configuration.StartingBytes, restored.Process.Bytes.Balance.Value);
            Assert.AreEqual(0, restored.Process.Counters.ExecutionsRun);
            Assert.AreEqual(0, restored.Executions.Count, "a restored Session has run nothing yet.");
            Assert.AreEqual(
                Occupied(restored.Process),
                restored.Process.ExposedInstanceIDs.Count,
                "the Buffer holds the initial load and nothing else."
            );
        }

        [Test]
        public void Restore_LeavesEveryRecordListEmptyWhileIdentitiesContinue()
        {
            ShippedCatalogSequenceDriver carried = ConfirmedPowerSurgeBranch();
            string expected = carried.Session.Economy.NextTransactionIdentity(TransactionKind.Purchase);
            ShippedCatalogSequenceDriver restored = RestoredFrom(carried);

            Assert.AreEqual(0, restored.Session.Repository.Records.Count);
            Assert.AreEqual(0, restored.Session.Economy.Records.Count);
            Assert.AreEqual(0, restored.Session.ActiveBranch.Records.Count);
            Assert.AreEqual("purchase:1", expected);
            Assert.AreEqual(
                carried.Session.Economy.NextTransactionIdentity(TransactionKind.Purchase),
                restored.Session.Economy.NextTransactionIdentity(TransactionKind.Purchase),
                "transaction identities continue from the captured counters rather than restarting."
            );
        }

        [Test]
        public void RecordFailure_SurvivesASaveAndLoad()
        {
            CheckpointState state = Capture(ConfirmedPowerSurgeBranch()).RecordFailure().RecordFailure();
            PersistenceService service = Service();

            service.SaveCheckpointAsync(state, CancellationToken.None).GetAwaiter().GetResult();
            LoadResult<CheckpointState> loaded = service.LoadCheckpointAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(LoadOutcome.Loaded, loaded.Outcome, loaded.Defect);
            Assert.AreEqual(2, loaded.Value.ConsecutiveFailures);
        }

        [Test]
        public void ACatalogRevisionBump_IsRejectedAndLeavesTheFileUntouched()
        {
            CheckpointState drifted = Capture(ConfirmedPowerSurgeBranch()) with { CatalogRevision = "0.0.0" };
            PersistenceService service = Service();
            service.SaveCheckpointAsync(drifted, CancellationToken.None).GetAwaiter().GetResult();
            string onDisk = File.ReadAllText(Path.Combine(_root, SaveFiles.Checkpoint));

            LoadResult<CheckpointState> loaded = service.LoadCheckpointAsync(CancellationToken.None).GetAwaiter().GetResult();
            CheckpointRestoreResult result = CheckpointRestore.Restore(loaded.Value, Catalog());

            Assert.AreEqual(LoadOutcome.Loaded, loaded.Outcome, "the file is readable; it is the content that no longer applies.");
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(CheckpointRestoreRejection.CatalogRevisionMismatch, result.Rejection);
            Assert.IsNull(result.Session);
            Assert.IsNull(result.Destination);
            Assert.AreEqual(onDisk, File.ReadAllText(Path.Combine(_root, SaveFiles.Checkpoint)));
        }

        [Test]
        public void Restore_BeforeTheCriticalProcess_MintsTheSameRuleIdentityAndNextAllocation()
        {
            // THERMAL THROTTLE names WB-PRC-001, so creating it allocates an identity from the Session's
            // source. This is the capture-moment contract asserted against the shipped content.
            ShippedCatalogSequenceDriver carried = ConfirmedCriticalBranch();
            ShippedCatalogSequenceDriver restored = RestoredFrom(carried);
            ProcessConfigurationDefinition configuration = Configuration(ThermalThrottle);

            Assert.IsTrue(carried.CreateProcess(configuration).Succeeded);
            Assert.IsTrue(restored.CreateProcess(configuration).Succeeded);

            Assert.IsNotNull(carried.Process.ProcessRule, "the Critical Process must name a rule, or this proves nothing.");
            Assert.AreEqual(carried.Process.ProcessRule.InstanceID, restored.Process.ProcessRule.InstanceID);
            Assert.AreEqual(carried.Session.InstanceIDs.Next(), restored.Session.InstanceIDs.Next());
        }

        /// <summary>
        /// The document text a checkpoint encodes to. Two checkpoints that encode identically agree on
        /// every member persistence carries, which is what a save round trip has to preserve.
        /// </summary>
        /// <param name="state">The checkpoint to encode.</param>
        /// <returns>The document text.</returns>
        private static string Encoded(CheckpointState state)
        {
            return JsonWriter.Write(CheckpointStateCodec.ToJson(state));
        }

        /// <summary>
        /// Captures a driver's Session at its Process entry, through the route that leads there.
        /// </summary>
        /// <param name="driver">The driver to capture.</param>
        /// <returns>The checkpoint.</returns>
        private static CheckpointState Capture(ShippedCatalogSequenceDriver driver)
        {
            CheckpointCaptureResult result = CheckpointCapture.Capture(
                driver.Session,
                new ProcessID(PowerSurge),
                new RouteID(PowerSurgeRoute),
                0
            );

            Assert.IsTrue(result.Succeeded, result.Rejection.ToString());
            return result.State;
        }

        /// <summary>
        /// Captures a driver, restores the checkpoint against the live catalog, and drives the restored
        /// Session — the whole round trip the child exists to prove.
        /// </summary>
        /// <param name="source">The driver to capture from.</param>
        /// <returns>A driver over the restored Session.</returns>
        private static ShippedCatalogSequenceDriver RestoredFrom(ShippedCatalogSequenceDriver source)
        {
            ContentCatalog catalog = Catalog();
            CheckpointRestoreResult result = CheckpointRestore.Restore(Capture(source), catalog);

            Assert.IsTrue(result.Succeeded, result.Detail);
            return new ShippedCatalogSequenceDriver(catalog, result.Session);
        }

        /// <summary>
        /// A persistence service over the real file adapter in this test's temporary directory.
        /// </summary>
        /// <returns>The service.</returns>
        private PersistenceService Service()
        {
            return new PersistenceService(
                new FileSaveStore(_root),
                new GameLoggerFactory(new ConsoleLogSink(), new LogFilterConfig(LogLevel.Warning))
            );
        }

        /// <summary>
        /// Reopens a Branch configuration, drops one non-Required member and confirms again.
        /// </summary>
        /// <param name="driver">The driver to act on.</param>
        /// <param name="configuration">The Process whose constraints apply.</param>
        private static void Reconfirm(
            ShippedCatalogSequenceDriver driver,
            ProcessConfigurationDefinition configuration
        )
        {
            driver.ConfigureBranch(configuration);
            IReadOnlyList<RepositoryEntry> entries = driver.Session.Repository.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                RepositoryEntry entry = entries[index];
                if (entry.Item.DefinitionID == ConditionValueEven)
                    continue;

                if (driver.Session.ActiveBranch.Exclude(entry.Item.InstanceID).Succeeded)
                    break;
            }

            Assert.IsTrue(driver.ConfirmBranch().Succeeded);
        }

        /// <summary>
        /// Credits enough Tokens for a reroll.
        /// </summary>
        /// <param name="driver">The driver to credit.</param>
        private static void Credit(ShippedCatalogSequenceDriver driver)
        {
            driver.Session.Economy.Tokens.Credit(new TokenAmount(9), TokenBasis.RewardTokens, "fixture");
        }

        /// <summary>
        /// Opens the pre-Critical shop and rerolls it once.
        /// </summary>
        /// <param name="driver">The driver to act on.</param>
        /// <returns>The reroll result.</returns>
        private static RerollResult OpenAndReroll(ShippedCatalogSequenceDriver driver)
        {
            ShopOpenResult opened = driver.OpenShop(new ShopID(PreCriticalShop), new ProcessID(ThermalThrottle));

            Assert.IsTrue(opened.Succeeded, opened.Rejection.ToString());
            return driver.Shop.Reroll();
        }

        /// <summary>
        /// The content identity in each of a shop's slots, in slot order.
        /// </summary>
        /// <param name="shop">The open shop.</param>
        /// <returns>The content identities.</returns>
        private static List<string> Contents(ShopState shop)
        {
            List<string> contents = new(shop.Slots.Count);
            for (int index = 0; index < shop.Slots.Count; index++)
            {
                contents.Add(shop.Slots[index].Offer.ContentID);
            }

            return contents;
        }

        /// <summary>
        /// Confirms a Branch for POWER SURGE over the shipped catalog, acquiring the Condition the Route
        /// Processes mark Required, which the starter archetype does not seed.
        /// </summary>
        /// <returns>The driver with a confirmed Branch.</returns>
        private static ShippedCatalogSequenceDriver ConfirmedPowerSurgeBranch()
        {
            ShippedCatalogSequenceDriver driver = Driver();
            AcquireCondition(driver);
            driver.ConfigureBranch(Configuration(PowerSurge));
            driver.TrimToCapacity();

            Assert.IsTrue(driver.ConfirmBranch().Succeeded);
            return driver;
        }

        /// <summary>
        /// Confirms a Branch for the Critical Process, which is where a Process rule is in play.
        /// </summary>
        /// <returns>The driver with a confirmed Branch.</returns>
        private static ShippedCatalogSequenceDriver ConfirmedCriticalBranch()
        {
            ShippedCatalogSequenceDriver driver = Driver();
            AcquireCondition(driver);
            driver.ConfigureBranch(Configuration(ThermalThrottle));
            driver.TrimToCapacity();

            Assert.IsTrue(driver.ConfirmBranch().Succeeded);
            return driver;
        }

        /// <summary>
        /// Acquires the Condition the Route Processes mark Required.
        /// </summary>
        /// <param name="driver">The driver whose Repository gains the Condition.</param>
        private static void AcquireCondition(ShippedCatalogSequenceDriver driver)
        {
            Assert.IsTrue(Catalog().TryGetStructure(new StructureID(ConditionValueEven), out StructureDefinition condition));
            Assert.IsTrue(driver.Session.Repository.Acquire(condition, AcquisitionOrigin.Reward).Succeeded);
        }

        /// <summary>
        /// Builds a driver over the real shipped catalog and its single starter archetype.
        /// </summary>
        /// <returns>The driver.</returns>
        private static ShippedCatalogSequenceDriver Driver()
        {
            ContentCatalog catalog = Catalog();

            Assert.AreEqual(1, catalog.StarterArchetypes.Count, "the shipped catalog ships one archetype.");
            return new ShippedCatalogSequenceDriver(
                catalog,
                catalog.StarterArchetypes[0],
                "WB-SYS-001",
                "session-1",
                "seed-1"
            );
        }

        /// <summary>
        /// Resolves an authored Process configuration from the shipped catalog by identity.
        /// </summary>
        /// <param name="id">The Process identity.</param>
        /// <returns>The configuration.</returns>
        private static ProcessConfigurationDefinition Configuration(string id)
        {
            Assert.IsTrue(
                Catalog().TryGetProcessConfiguration(new ProcessID(id), out ProcessConfigurationDefinition configuration),
                id
            );

            return configuration;
        }

        /// <summary>
        /// The candidate identities a Process's exposure draws selected, in draw order.
        /// </summary>
        /// <param name="process">The created Process.</param>
        /// <returns>The selected identities.</returns>
        private static List<string> SelectedIdentities(ProcessState process)
        {
            List<string> identities = new();
            for (int index = 0; index < process.ExposureRecords.Count; index++)
            {
                IReadOnlyList<string> selected = process.ExposureRecords[index].Outcome.SelectedIdentities;
                for (int pick = 0; pick < selected.Count; pick++)
                {
                    identities.Add(selected[pick]);
                }
            }

            return identities;
        }

        /// <summary>
        /// The draw positions a Process consumed, in order.
        /// </summary>
        /// <param name="process">The created Process.</param>
        /// <returns>The occurrence ordinals.</returns>
        private static List<int> Positions(ProcessState process)
        {
            List<int> positions = new();
            for (int index = 0; index < process.ExposureRecords.Count; index++)
            {
                positions.Add(process.ExposureRecords[index].Request.Context.Components.OccurrenceOrdinal);
            }

            return positions;
        }

        /// <summary>
        /// How many Buffer slots hold an item. Slots are capacity-sized and an open slot holds null.
        /// </summary>
        /// <param name="process">The created Process.</param>
        /// <returns>The occupied slot count.</returns>
        private static int Occupied(ProcessState process)
        {
            int occupied = 0;
            IReadOnlyList<BufferSlot> slots = process.Buffer.Slots;
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index].Item != null)
                    occupied += 1;
            }

            return occupied;
        }

        /// <summary>
        /// Loads the real shipped catalog through the same pipeline as the standing conformance test.
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
