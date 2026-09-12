using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the position-indexed exposure draw: one Determinism decision per arrival position over the
    /// unseen, non-guaranteed confirmed Branch, guaranteed content resolved before any draw and kept out
    /// of every denominator, and a Process-scoped position counter starting at one. Retry determinism is
    /// asserted structurally — a second Process in the same Session reproduces the sequence because the
    /// counter restarts with the Process, never because anything is reset.
    /// </summary>
    public sealed class ExposureDrawTests
    {
        [Test]
        public void DrawNext_DeclaresTheDecisionTheSpecificationStates()
        {
            ProcessState process = CreateProcessThree().State;

            DecisionRequest request = process.ExposureRecords[0].Request;

            Assert.AreEqual("Active Branch exposure", request.SelectionPurpose);
            Assert.AreEqual(SelectionMethod.UniformSingleSelection, request.Method);
            Assert.AreEqual(1, request.SelectionCount);
            Assert.AreEqual(ReplacementBehavior.RemovedFromLongerLivedPopulation, request.Replacement);
            Assert.AreEqual(InsufficientCandidateBehavior.CancelTheDecision, request.InsufficientCandidates);
        }

        [Test]
        public void DrawNext_BuildsTheContextFromTheSessionAndProcessIdentities()
        {
            ProcessState process = CreateProcessThree().State;

            DecisionContextComponents components = process.ExposureRecords[0].Request.Context.Components;

            Assert.AreEqual("seed-1", components.SessionSeedIdentity);
            Assert.AreEqual("fixture-0.0.1", components.ContentRevision);
            Assert.AreEqual(DeterminismService.RevisionIdentity, components.RulesetRevision);
            Assert.AreEqual("WB-SYS-001", components.SystemIdentity);
            Assert.AreEqual("WB-PROC-003", components.ProcessIdentity);
            Assert.AreEqual("PROCESS", components.ScopeIdentity);
            Assert.IsNull(components.CausingEventIdentity);
            Assert.IsNull(components.EffectOriginIdentity);
            Assert.AreEqual("Active Branch exposure", components.SelectionPurpose);
        }

        [Test]
        public void EveryDeclarationString_IsNonEmptyAndThePurposeIsRecognized()
        {
            ProcessState process = CreateProcessThree().State;

            IReadOnlyList<RandomDecisionRecord> records = process.ExposureRecords;
            Assert.Greater(records.Count, 0, "the fixture draws at least once.");
            for (int index = 0; index < records.Count; index++)
            {
                DecisionRequest request = records[index].Request;
                Assert.IsTrue(SelectionPurposes.All.Contains(request.SelectionPurpose), "purpose");
                Assert.IsFalse(string.IsNullOrEmpty(request.SelectionIdentity), "selection identity");
                Assert.IsFalse(string.IsNullOrEmpty(request.SelectionBoundary), "selection boundary");
                Assert.IsFalse(string.IsNullOrEmpty(request.CandidateSource), "candidate source");
                Assert.IsFalse(string.IsNullOrEmpty(request.EligibilityRule), "eligibility rule");
                Assert.IsFalse(string.IsNullOrEmpty(request.CandidateSnapshotTiming), "snapshot timing");
                Assert.IsFalse(string.IsNullOrEmpty(request.CandidateOrderingRule), "ordering rule");
                Assert.IsFalse(string.IsNullOrEmpty(request.TieBreakRule), "tie-break rule");
            }
        }

        [Test]
        public void SelectionIdentities_AreDistinctAcrossTheProcessesDraws()
        {
            ProcessState process = DriveProcessThreeToCompletion();

            HashSet<string> identities = new(StringComparer.Ordinal);
            for (int index = 0; index < process.ExposureRecords.Count; index++)
                identities.Add(process.ExposureRecords[index].Request.SelectionIdentity);

            Assert.AreEqual(process.ExposureRecords.Count, identities.Count, "one identity per decision.");
        }

        [Test]
        public void Positions_StartAtOneAndRiseByOnePerDraw()
        {
            ProcessState process = DriveProcessThreeToCompletion();

            IReadOnlyList<RandomDecisionRecord> records = process.ExposureRecords;
            Assert.AreEqual(5, records.Count, "two initial draws and one per later moment.");
            for (int index = 0; index < records.Count; index++)
            {
                Assert.AreEqual(
                    index + 1,
                    records[index].Request.Context.Components.OccurrenceOrdinal,
                    "position " + (index + 1).ToString()
                );
            }
        }

        [Test]
        public void GuaranteedContent_ArrivesFirstAndNeverEntersACandidateSnapshot()
        {
            ProcessState process = DriveProcessThreeToCompletion();

            string guaranteed = new InstanceID(7).ToString();
            Assert.AreEqual(
                guaranteed,
                process.Buffer.Slots[0].Item.InstanceID.ToString(),
                "the guaranteed item takes the first slot, before any draw."
            );

            for (int index = 0; index < process.ExposureRecords.Count; index++)
            {
                IReadOnlyList<CandidateEntry> evidence = process.ExposureRecords[index].CandidateEvidence;
                for (int candidate = 0; candidate < evidence.Count; candidate++)
                {
                    Assert.AreNotEqual(
                        guaranteed,
                        evidence[candidate].Identity,
                        "a guaranteed instance is never in a random denominator (CAB-EVT-865)."
                    );
                }
            }
        }

        [Test]
        public void Create_WithABranchLackingGuaranteedContent_IsRejectedByName()
        {
            SessionState session = ProcessThreeSession();
            ActiveBranchConfiguration branch = BranchOf(session, 1, 2, 3, 4);

            ProcessCreationResult result = Create(session, ProcessThreeConfiguration(), branch);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ProcessCreationRejection.GuaranteedContentMissing, result.Rejection);
        }

        [Test]
        public void AnInSessionRetry_ReproducesTheIdenticalArrivalSequence()
        {
            SessionState session = ProcessThreeSession();
            ActiveBranchConfiguration branch = FullBranch(session);

            ProcessState first = DriveToCompletion(Create(session, ProcessThreeConfiguration(), branch).State);
            ProcessState second = DriveToCompletion(Create(session, ProcessThreeConfiguration(), branch).State);

            CollectionAssert.AreEqual(SelectedIdentities(first), SelectedIdentities(second));
        }

        [Test]
        public void TwoSessionsOnTheSameSeed_ReproduceTheIdenticalArrivalSequence()
        {
            ProcessState first = DriveProcessThreeToCompletion();
            ProcessState second = DriveProcessThreeToCompletion();

            CollectionAssert.AreEqual(SelectedIdentities(first), SelectedIdentities(second));
        }

        [Test]
        public void AModifiedBranch_ReusesTheSamePositionsOverADifferentComposition()
        {
            SessionState session = ProcessThreeSession();
            ProcessState unchanged = DriveToCompletion(
                Create(session, ProcessThreeConfiguration(), FullBranch(session)).State
            );
            ProcessState modified = DriveToCompletion(
                Create(session, ProcessThreeConfiguration(), BranchOf(session, 1, 2, 4, 5, 7, 8, 9)).State
            );

            CollectionAssert.AreEqual(
                Positions(unchanged),
                Positions(modified),
                "the deterministic draw positions are reused (UX-ERR-013, CAB-EVT-886)."
            );
            CollectionAssert.AreNotEqual(
                SelectedIdentities(unchanged),
                SelectedIdentities(modified),
                "over the new composition."
            );
        }

        [Test]
        public void AnEmptyPopulation_CancelsTheDecisionAndAdmitsNothing()
        {
            SessionState session = ProcessThreeSession();
            ActiveBranchConfiguration branch = BranchOf(session, 7);

            ProcessCreationResult result = Create(session, ProcessThreeConfiguration(initialCount: 2), branch);

            Assert.IsTrue(result.Succeeded, result.Rejection.ToString());
            ProcessState process = result.State;
            Assert.AreEqual(1, process.ExposureRecords.Count, "the cancelled decision is still recorded.");
            Assert.AreEqual(
                DecisionDisposition.Cancelled,
                process.ExposureRecords[0].Outcome.Disposition
            );
            Assert.AreEqual(0, process.ExposureRecords[0].Draws.Count, "no implicit partial draw (CAB-EVT-790).");
            Assert.AreEqual(1, Occupied(process), "only the guaranteed item arrived.");
        }

        [Test]
        public void ExposureRecords_AreRetainedInDrawOrder()
        {
            ProcessState process = DriveProcessThreeToCompletion();

            IReadOnlyList<RandomDecisionRecord> records = process.ExposureRecords;
            for (int index = 1; index < records.Count; index++)
            {
                Assert.Less(
                    records[index - 1].Request.Context.Components.OccurrenceOrdinal,
                    records[index].Request.Context.Components.OccurrenceOrdinal,
                    "records are held in draw order."
                );
            }
        }

        [Test]
        public void ProcessThree_ExposesSixDistinctItemsAndNeverASeventh()
        {
            ProcessState process = DriveProcessThreeToCompletion();

            HashSet<int> exposed = new();
            for (int index = 0; index < process.ExposedInstanceIDs.Count; index++)
                exposed.Add(process.ExposedInstanceIDs[index].Value);

            Assert.AreEqual(6, exposed.Count, "one guaranteed and five drawn, all distinct.");

            ArrivalResult beyond = process.Arrive(new ArrivalMoment(4));

            Assert.IsFalse(beyond.Succeeded, "nothing arrives after the final execution.");
            Assert.AreEqual(6, process.ExposedInstanceIDs.Count, "and no seventh instance is exposed.");
        }

        [Test]
        public void ASelectedInstance_IsNeverACandidateAgainInTheSameProcess()
        {
            ProcessState process = DriveProcessThreeToCompletion();

            HashSet<string> selected = new(StringComparer.Ordinal);
            IReadOnlyList<RandomDecisionRecord> records = process.ExposureRecords;
            for (int index = 0; index < records.Count; index++)
            {
                IReadOnlyList<CandidateEntry> evidence = records[index].CandidateEvidence;
                for (int candidate = 0; candidate < evidence.Count; candidate++)
                {
                    Assert.IsFalse(
                        selected.Contains(evidence[candidate].Identity),
                        "a drawn instance cannot naturally reappear (CAB-EVT-866)."
                    );
                }

                IReadOnlyList<string> chosen = records[index].Outcome.SelectedIdentities;
                for (int pick = 0; pick < chosen.Count; pick++)
                    selected.Add(chosen[pick]);
            }
        }

        [Test]
        public void AScriptedProcess_ConstructsNoDrawAndConsumesNoPosition()
        {
            ProcessState process = CreateProcessThree(scripted: true).State;

            Assert.AreEqual(0, process.ExposureRecords.Count, "scripted Processes never draw (spec 4.4).");
        }

        /// <summary>
        /// Creates the Process 3 fixture over a freshly seeded Session and its full Branch.
        /// </summary>
        /// <param name="scripted">Whether the configuration uses a scripted load instead of a drawn one.</param>
        /// <returns>The creation result.</returns>
        private static ProcessCreationResult CreateProcessThree(bool scripted = false)
        {
            SessionState session = ProcessThreeSession();
            return Create(session, ProcessThreeConfiguration(scripted: scripted), FullBranch(session));
        }

        /// <summary>
        /// Creates the Process 3 fixture and fires every later arrival moment.
        /// </summary>
        /// <returns>The driven Process.</returns>
        private static ProcessState DriveProcessThreeToCompletion()
        {
            return DriveToCompletion(CreateProcessThree().State);
        }

        /// <summary>
        /// Fires the three later arrival moments in order.
        /// </summary>
        /// <param name="process">The created Process.</param>
        /// <returns>The same Process, driven.</returns>
        private static ProcessState DriveToCompletion(ProcessState process)
        {
            for (int execution = 1; execution <= 3; execution++)
                process.Arrive(new ArrivalMoment(execution));

            return process;
        }

        /// <summary>
        /// Creates a Process against the fixture catalog, resolving its setup first.
        /// </summary>
        /// <param name="session">The seeded Session.</param>
        /// <param name="configuration">The Process configuration.</param>
        /// <param name="branch">The confirmed Active Branch.</param>
        /// <returns>The creation result.</returns>
        private static ProcessCreationResult Create(
            SessionState session,
            ProcessConfigurationDefinition configuration,
            ActiveBranchConfiguration branch
        )
        {
            ContentCatalog catalog = ProgressionFixtures.ProcessCatalog();
            ProcessSetup setup = ProcessSetupResolver.Resolve(
                configuration,
                catalog.Parameters,
                Array.Empty<ActiveSetupEffect>()
            );

            return ProcessState.Create(configuration, setup, catalog, session, branch);
        }

        /// <summary>
        /// Seeds a Session holding nine Branch-eligible items: three copies of one Instruction, three of
        /// a second, then the scoring Instruction, a Structure and a Directive — so instances one to nine
        /// are the Branch population and instance seven is the guaranteed item.
        /// </summary>
        /// <returns>The seeded Session.</returns>
        private static SessionState ProcessThreeSession()
        {
            return SessionState.Create(
                ProgressionFixtures.ProcessCatalog(),
                ProgressionFixtures.Archetype(
                    ProgressionFixtures.ValuePlusTwo,
                    ProgressionFixtures.ValuePlusTwo,
                    ProgressionFixtures.ValuePlusTwo,
                    ProgressionFixtures.ValuePlusThree,
                    ProgressionFixtures.ValuePlusThree,
                    ProgressionFixtures.ValuePlusThree,
                    ProgressionFixtures.ScorePlusValue,
                    ProgressionFixtures.RepeatTwo,
                    ProgressionFixtures.Overclock
                ),
                "WB-SYS-001",
                "session-1",
                "seed-1"
            );
        }

        /// <summary>
        /// The confirmed Branch holding every seeded item.
        /// </summary>
        /// <param name="session">The seeded Session.</param>
        /// <returns>The Branch configuration.</returns>
        private static ActiveBranchConfiguration FullBranch(SessionState session)
        {
            return BranchOf(session, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        }

        /// <summary>
        /// Builds a confirmed Branch over the named instance identities.
        /// </summary>
        /// <param name="session">The seeded Session, whose Repository holds the instances.</param>
        /// <param name="instances">The instance identity numbers to include.</param>
        /// <returns>The Branch configuration.</returns>
        private static ActiveBranchConfiguration BranchOf(SessionState session, params int[] instances)
        {
            List<InstanceID> eligible = new(instances.Length);
            for (int index = 0; index < instances.Length; index++)
            {
                InstanceID id = new(instances[index]);
                Assert.IsTrue(session.Repository.Contains(id), "the fixture Branch names a held instance.");
                eligible.Add(id);
            }

            return new ActiveBranchConfiguration(eligible);
        }

        /// <summary>
        /// Builds the Process 3 configuration: a nine-item Branch, one guaranteed item, three initial
        /// Buffer items and three later arrival moments.
        /// </summary>
        /// <param name="initialCount">The number of items loaded at Process start.</param>
        /// <param name="scripted">Whether the load is scripted rather than drawn.</param>
        /// <returns>The configuration.</returns>
        private static ProcessConfigurationDefinition ProcessThreeConfiguration(
            int initialCount = 3,
            bool scripted = false
        )
        {
            BufferLoadSpec load = scripted
                ? new BufferLoadSpec(
                    BufferLoadPolicy.Scripted,
                    new[] { ProgressionFixtures.ValuePlusTwo },
                    Array.Empty<ArrivalLoadSpec>(),
                    0,
                    Array.Empty<int>())
                : new BufferLoadSpec(
                    BufferLoadPolicy.Drawn,
                    Array.Empty<string>(),
                    Array.Empty<ArrivalLoadSpec>(),
                    initialCount,
                    new[] { 1, 2, 3 });

            return new ProcessConfigurationDefinition(
                new ProcessID("WB-PROC-003"),
                "Process 3",
                ProcessRole.RouteProcess3,
                new CoreID("WB-CORE-002"),
                null,
                new ProcessThresholdSpec(18, 32, 80),
                4,
                false,
                6,
                8,
                5,
                Array.Empty<InitialSourceSpec>(),
                load,
                scripted ? null : new ExposureSpec(new[] { ProgressionFixtures.ScorePlusValue }),
                new ActiveBranchSpec(9, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()),
                new RewardPackageID("WB-RWD-003"),
                null
            );
        }

        /// <summary>
        /// The number of Buffer slots holding an item. Slots are capacity-sized and an open slot holds
        /// null, so occupancy is a count rather than the slot list's length.
        /// </summary>
        /// <param name="process">The Process whose Buffer is counted.</param>
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
        /// The identities selected across a Process's exposure records, in draw order.
        /// </summary>
        /// <param name="process">The driven Process.</param>
        /// <returns>The selected candidate identities.</returns>
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
        /// <param name="process">The driven Process.</param>
        /// <returns>The occurrence ordinals.</returns>
        private static List<int> Positions(ProcessState process)
        {
            List<int> positions = new();
            for (int index = 0; index < process.ExposureRecords.Count; index++)
                positions.Add(process.ExposureRecords[index].Request.Context.Components.OccurrenceOrdinal);

            return positions;
        }
    }
}
