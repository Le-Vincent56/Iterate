using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using Iterate.Infrastructure.Content;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// The child's end-to-end suite against the real shipped catalog, driven through
    /// <see cref="ShippedCatalogSequenceDriver"/>: the authored Processes actually open, load, compile,
    /// execute and overflow the way Design and Balance say they do. The Domain suites prove the units
    /// over hand-built fixtures; this one proves the shipped content is authored consistently with them.
    /// </summary>
    public sealed class ShippedCatalogProgressionTests
    {
        private const string TutorialOne = "WB-PROC-001";

        private const string TutorialTwo = "WB-PROC-002";

        private const string ParityCheck = "WB-PROC-003";

        private const string PowerSurge = "WB-PROC-004";

        private const string ThermalThrottle = "WB-PROC-005";

        private const string ConditionValueEven = "WB-STR-002";

        private const string Overclock = "WB-DIR-001";

        [Test]
        public void TutorialOne_RunsThreeEditedCompilationsAtZeroBytes()
        {
            ShippedCatalogSequenceDriver driver = Driver();
            Assert.IsTrue(driver.CreateProcess(Configuration(driver, TutorialOne)).Succeeded);
            Assert.AreEqual(0, driver.Process.Bytes.Balance.Value, "Tutorial 1 opens with no Bytes at all.");

            CompilationAttempt opening = driver.Compile();
            Assert.AreEqual(
                CompilationClassification.Initial,
                opening.Breakdown.Classification,
                "the opening commit is the Initial compilation, however the arrangement was reached."
            );
            driver.Execute(opening.Source);

            List<CompilationAttempt> edited = new();

            ArrivalResult first = driver.Arrive(1);
            Assert.IsTrue(first.Succeeded);
            InstanceID arrived = first.Arrived[0].InstanceID;
            Assert.IsTrue(driver.Install(arrived, 5).Succeeded);
            edited.Add(driver.Compile());

            Assert.IsTrue(driver.Remove(5).Succeeded);
            Assert.IsTrue(driver.Install(arrived, 6).Succeeded);
            edited.Add(driver.Compile());
            driver.Execute(edited[1].Source);

            ArrivalResult second = driver.Arrive(2);
            Assert.IsTrue(second.Succeeded);
            Assert.IsTrue(driver.Install(second.Arrived[0].InstanceID, 5).Succeeded);
            edited.Add(driver.Compile());
            driver.Execute(edited[2].Source);

            Assert.AreEqual(3, edited.Count);
            for (int index = 0; index < edited.Count; index++)
            {
                CompilationAttempt attempt = edited[index];
                Assert.IsTrue(attempt.Committed, "compilation " + (index + 1).ToString() + ": " + attempt.BlockReason);
                Assert.AreEqual(CompilationClassification.OrdinaryEdited, attempt.Breakdown.Classification);
                Assert.AreEqual(0, attempt.Breakdown.FinalCost, "TUTORIAL FREE COMPILATION zeroes every edited compilation.");
            }

            Assert.AreEqual(0, driver.Process.Bytes.Balance.Value, "so nothing was ever debited.");
            Assert.AreEqual(3, driver.Process.Counters.ExecutionsRun);
        }

        [Test]
        public void TutorialOnesCompilationOnlyRule_LeavesNoProcessCounterEvidence()
        {
            ShippedCatalogSequenceDriver driver = Driver();
            driver.CreateProcess(Configuration(driver, TutorialOne));
            CompilationAttempt attempt = driver.Compile();

            ExecutionRecord record = driver.Execute(attempt.Source);

            Assert.IsNotNull(driver.Process.ProcessRule, "the Process state exposes WB-PRC-002 unconditionally.");
            Assert.IsNull(
                driver.BuildRequest(attempt.Source).Configuration.ProcessRule,
                "but a COMPILATION-only rule is withheld from the engine."
            );
            Assert.AreEqual(0, record.CounterHistory.Count, "so no Process-counter evidence is fabricated.");
        }

        [Test]
        public void TutorialTwo_OverflowsAtTheExecutionThreeArrival()
        {
            ShippedCatalogSequenceDriver driver = DriveTutorialTwoToOverflow();

            Assert.IsTrue(driver.Process.Buffer.IsOverflowing, "two items arrive into a full three-slot Buffer.");
            Assert.AreEqual(2, QueueDepth(driver), "both authored arrivals are held outside the Buffer.");
            Assert.AreEqual(
                Overclock,
                driver.Process.Buffer.IncomingItem.DefinitionID,
                "the Directive is first in authored order, so it is the incoming item."
            );
        }

        [Test]
        public void TutorialTwoOverflow_IsExitedByArchivingTheIncomingItem()
        {
            ShippedCatalogSequenceDriver driver = DriveTutorialTwoToOverflow();
            int before = QueueDepth(driver);

            Assert.IsTrue(driver.Process.Buffer.ArchiveIncoming().Succeeded);

            Assert.AreEqual(before - 1, QueueDepth(driver), "archiving the incoming item is always legal.");
        }

        [Test]
        public void TutorialTwoOverflow_IsAlwaysResolvableByRepeatedArchiving()
        {
            ShippedCatalogSequenceDriver driver = DriveTutorialTwoToOverflow();
            int exits = 0;

            while (driver.Process.Buffer.IsOverflowing && exits < 10)
            {
                Assert.IsTrue(driver.Process.Buffer.ArchiveIncoming().Succeeded);
                exits += 1;
            }

            Assert.IsFalse(driver.Process.Buffer.IsOverflowing, "every authored overflow state has a legal exit.");
            Assert.AreEqual(2, exits, "one exit per held item, and no more.");
        }

        [Test]
        public void TutorialTwoOverflow_IsExitedByArchivingAResident()
        {
            ShippedCatalogSequenceDriver driver = DriveTutorialTwoToOverflow();
            int before = QueueDepth(driver);
            RepositoryItem incoming = driver.Process.Buffer.IncomingItem;

            Assert.IsTrue(driver.Process.Buffer.Archive(FirstOccupant(driver)).Succeeded);

            Assert.AreEqual(before - 1, QueueDepth(driver), "the freed slot admits the queue head.");
            Assert.AreEqual(
                BufferItemState.Present,
                driver.Process.Buffer.StateOf(incoming.InstanceID),
                "and the item that was held is now in the Buffer."
            );
        }

        [Test]
        public void TutorialTwoOverflow_IsExitedByInstallingABufferedItem()
        {
            ShippedCatalogSequenceDriver driver = DriveTutorialTwoToOverflow();
            int before = QueueDepth(driver);

            Assert.IsTrue(driver.Install(FirstInstallableOccupant(driver), 7).Succeeded);

            Assert.AreEqual(
                before - 1,
                QueueDepth(driver),
                "the drain runs inside Compilation's own edit, so an install exits overflow."
            );
        }

        [Test]
        public void AnArchivedDirective_IsStillAvailableToTheNextProcess()
        {
            ShippedCatalogSequenceDriver driver = DriveTutorialTwoToOverflow();
            InstanceID directive = driver.Process.Buffer.IncomingItem.InstanceID;
            Assert.IsTrue(driver.Process.Buffer.ArchiveIncoming().Succeeded);
            Assert.AreEqual(BufferItemState.Archived, driver.Process.Buffer.StateOf(directive));

            Assert.IsTrue(driver.CreateProcess(Configuration(driver, TutorialOne)).Succeeded);

            Assert.IsTrue(
                driver.Session.Repository.TryResolveContent(Overclock, driver.Process.ExposedInstanceIDs, out RepositoryEntry entry),
                "an archived Directive leaves the Buffer, never the Repository."
            );
            Assert.AreEqual(directive, entry.Item.InstanceID);
        }

        [Test]
        public void TheFirstBranchConfiguration_OpensAtTenOfNineWithTheConditionRequired()
        {
            ShippedCatalogSequenceDriver driver = Driver();
            AcquireCondition(driver);

            BranchValidation validation = driver.ConfigureBranch(Configuration(driver, PowerSurge));

            Assert.AreEqual(10, validation.SelectedCount, "nine starter items plus the acquired Condition.");
            Assert.AreEqual(9, validation.Capacity);
            Assert.IsTrue(validation.IsOverCapacity, "which the player edits down, rather than a failed edit.");
            Assert.AreEqual(0, validation.MissingRequired.Count, "the Required Condition is already selected.");
        }

        [Test]
        public void AnOverCapacityDraft_ConfirmsOnceTrimmedToCapacity()
        {
            ShippedCatalogSequenceDriver driver = Driver();
            AcquireCondition(driver);
            driver.ConfigureBranch(Configuration(driver, PowerSurge));

            BranchValidation trimmed = driver.TrimToCapacity();

            Assert.IsTrue(trimmed.IsValid, "over capacity is a draft state, not a dead end.");
            Assert.IsTrue(driver.ConfirmBranch().Succeeded);
        }

        [Test]
        public void PowerSurge_ReproducesItsArrivalsOnAnInSessionRetry()
        {
            ShippedCatalogSequenceDriver driver = ConfirmedPowerSurgeBranch();
            ProcessConfigurationDefinition configuration = Configuration(driver, PowerSurge);

            Assert.IsTrue(driver.CreateProcess(configuration).Succeeded);
            List<string> first = SelectedIdentities(driver.Process);

            Assert.IsTrue(driver.CreateProcess(configuration).Succeeded);
            List<string> second = SelectedIdentities(driver.Process);

            Assert.Greater(first.Count, 0, "the drawn load draws at Process start.");
            CollectionAssert.AreEqual(first, second, "the position counter restarts with the Process.");
        }

        [Test]
        public void PowerSurge_ReusesItsDrawPositionsOverAModifiedBranch()
        {
            ShippedCatalogSequenceDriver unchanged = ConfirmedPowerSurgeBranch();
            Assert.IsTrue(unchanged.CreateProcess(Configuration(unchanged, PowerSurge)).Succeeded);

            ShippedCatalogSequenceDriver modified = ConfirmedPowerSurgeBranch(dropOne: true);
            Assert.IsTrue(modified.CreateProcess(Configuration(modified, PowerSurge)).Succeeded);

            CollectionAssert.AreEqual(
                Positions(unchanged.Process),
                Positions(modified.Process),
                "a changed Branch reuses the same deterministic positions."
            );
        }

        [Test]
        public void ThermalThrottle_SetsUpAtBufferFour()
        {
            ShippedCatalogSequenceDriver driver = Driver();
            ProcessConfigurationDefinition configuration = Configuration(driver, ThermalThrottle);

            ProcessSetup setup = ProcessSetupResolver.Resolve(
                configuration,
                Catalog().Parameters,
                System.Array.Empty<ActiveSetupEffect>()
            );

            Assert.AreEqual(4, setup.BufferCapacity, "the Critical Process opens one slot tighter.");
            Assert.AreEqual(9, setup.BranchCapacity);
        }

        [Test]
        public void ParityCheck_IsRejectedByNameForItsFixedStructureCore()
        {
            ShippedCatalogSequenceDriver driver = ConfirmedPowerSurgeBranch();

            ProcessCreationResult result = driver.CreateProcess(Configuration(driver, ParityCheck));

            Assert.IsFalse(result.Succeeded, "PARITY CHECK's Core carries a fixed Structure line.");
            Assert.AreEqual(
                ProcessCreationRejection.CoreStructureUnsupported,
                result.Rejection,
                "named rather than worked around; the Core source objects child lifts it."
            );
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
        /// <param name="driver">The driver, for its catalog revision assertions.</param>
        /// <param name="id">The Process identity.</param>
        /// <returns>The configuration.</returns>
        private static ProcessConfigurationDefinition Configuration(ShippedCatalogSequenceDriver driver, string id)
        {
            Assert.IsNotNull(driver);
            Assert.IsTrue(
                Catalog().TryGetProcessConfiguration(new ProcessID(id), out ProcessConfigurationDefinition configuration),
                id
            );
            return configuration;
        }

        /// <summary>
        /// Runs Tutorial 2 to the two-item arrival that overflows its three-slot Buffer, installing one
        /// buffered Instruction on the way so the Buffer drains the way play drains it rather than
        /// silting up from the first moment. The authored arrival after execution three is a Directive
        /// then an Instruction, so both are held and the Directive is the incoming one.
        /// </summary>
        /// <returns>The driver, with the Process overflowing.</returns>
        private static ShippedCatalogSequenceDriver DriveTutorialTwoToOverflow()
        {
            ShippedCatalogSequenceDriver driver = Driver();
            Assert.IsTrue(driver.CreateProcess(Configuration(driver, TutorialTwo)).Succeeded);

            CompilationAttempt opening = driver.Compile();
            driver.Execute(opening.Source);

            Assert.IsTrue(driver.Arrive(1).Succeeded);
            Assert.IsTrue(driver.Install(FirstInstallableOccupant(driver), 6).Succeeded);

            CompilationAttempt second = driver.Compile();
            Assert.IsTrue(second.Committed, second.BlockReason.ToString());
            driver.Execute(second.Source);

            Assert.IsTrue(driver.Arrive(2).Succeeded);
            driver.Execute(second.Source);
            Assert.IsTrue(driver.Arrive(3).Succeeded);

            return driver;
        }

        /// <summary>
        /// How many items the Buffer holds outside its slots: the incoming one plus those queued behind
        /// it, or zero when the Buffer is not overflowing.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <returns>The held-item count.</returns>
        private static int QueueDepth(ShippedCatalogSequenceDriver driver)
        {
            InstructionBuffer buffer = driver.Process.Buffer;
            return buffer.IsOverflowing ? 1 + buffer.PendingArrivals.Count : 0;
        }

        /// <summary>
        /// Confirms a Branch for POWER SURGE, optionally dropping one non-Required instance so the
        /// modified-Branch case runs over a different composition at the same positions.
        /// </summary>
        /// <param name="dropOne">Whether to exclude one further instance before confirming.</param>
        /// <returns>The driver with a confirmed Branch.</returns>
        private static ShippedCatalogSequenceDriver ConfirmedPowerSurgeBranch(bool dropOne = false)
        {
            ShippedCatalogSequenceDriver driver = Driver();
            AcquireCondition(driver);
            driver.ConfigureBranch(Configuration(driver, PowerSurge));
            driver.TrimToCapacity();

            if (dropOne)
            {
                IReadOnlyList<RepositoryEntry> entries = driver.Session.Repository.Entries;
                for (int index = 0; index < entries.Count; index++)
                {
                    RepositoryEntry entry = entries[index];
                    if (entry.Item.DefinitionID == ConditionValueEven)
                        continue;

                    if (driver.Session.ActiveBranch.Exclude(entry.Item.InstanceID).Succeeded)
                        break;
                }
            }

            Assert.IsTrue(driver.ConfirmBranch().Succeeded);
            return driver;
        }

        /// <summary>
        /// Acquires the Condition the Route Processes mark Required, which the starter archetype does
        /// not seed — the reward that grants it belongs to the shop and rewards child.
        /// </summary>
        /// <param name="driver">The driver whose Repository gains the Condition.</param>
        private static void AcquireCondition(ShippedCatalogSequenceDriver driver)
        {
            Assert.IsTrue(Catalog().TryGetStructure(new StructureID(ConditionValueEven), out StructureDefinition condition));
            Assert.IsTrue(driver.Session.Repository.Acquire(condition, AcquisitionOrigin.Reward).Succeeded);
        }

        /// <summary>
        /// The index of the first occupied Buffer slot.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <returns>The slot index.</returns>
        private static int FirstOccupiedSlot(ShippedCatalogSequenceDriver driver)
        {
            IReadOnlyList<BufferSlot> slots = driver.Process.Buffer.Slots;
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index].Item != null)
                    return index;
            }

            Assert.Fail("the Buffer holds no item.");
            return -1;
        }

        /// <summary>
        /// The instance in the first occupied Buffer slot.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <returns>The instance identity.</returns>
        private static InstanceID FirstOccupant(ShippedCatalogSequenceDriver driver)
        {
            return driver.Process.Buffer.Slots[FirstOccupiedSlot(driver)].Item.InstanceID;
        }

        /// <summary>
        /// The first buffered instance that can be installed into the source — an Instruction or a
        /// Structure, never a Directive, which is consumed rather than installed.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <returns>The instance identity.</returns>
        private static InstanceID FirstInstallableOccupant(ShippedCatalogSequenceDriver driver)
        {
            IReadOnlyList<BufferSlot> slots = driver.Process.Buffer.Slots;
            for (int index = 0; index < slots.Count; index++)
            {
                BufferSlot slot = slots[index];
                if (slot.Item != null && slot.Item.Kind == RepositoryItemKind.Instruction)
                    return slot.Item.InstanceID;
            }

            Assert.Fail("the Buffer holds no installable item.");
            return default;
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
                    identities.Add(selected[pick]);
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
                positions.Add(process.ExposureRecords[index].Request.Context.Components.OccurrenceOrdinal);

            return positions;
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
