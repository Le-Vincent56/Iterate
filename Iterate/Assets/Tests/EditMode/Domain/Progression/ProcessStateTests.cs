using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests Process creation end to end for a scripted Process: the Core materialised, the initial
    /// source installed through Compilation's own edit rather than a second construction path, the
    /// Buffer and ledger built at their resolved values, and scripted arrivals firing once each in
    /// authored order. The Process rule's compilation effects are produced here so the Compilation
    /// caller has the list it passes to <c>Compile</c>.
    /// </summary>
    public sealed class ProcessStateTests
    {
        [Test]
        public void Create_MaterialisesTheCoreAndBuildsTheBufferAndLedger()
        {
            ProcessCreationResult result = CreateTutorialTwo();

            Assert.IsTrue(result.Succeeded, result.Rejection.ToString());
            ProcessState process = result.State;
            Assert.AreEqual(3, process.Buffer.Capacity);
            Assert.AreEqual(3, process.Bytes.Balance.Value);
            Assert.AreEqual(4, process.Counters.Allowance);
            Assert.AreEqual(8, process.InitialArrangement.Slots.Count);
        }

        [Test]
        public void Create_InstallsTheInitialSourceThroughCompilationsOwnEdit()
        {
            ProcessCreationResult result = CreateTutorialTwo();
            SourceArrangement produced = result.State.InitialArrangement;

            SourceArrangement byHand = BuildByHand();

            Assert.AreEqual(byHand.Slots.Count, produced.Slots.Count);
            for (int index = 0; index < byHand.Slots.Count; index++)
            {
                Assert.AreEqual(
                    byHand.Slots[index].Kind,
                    produced.Slots[index].Kind,
                    "slot " + (index + 1) + " kind"
                );
            }
        }

        [Test]
        public void Create_PutsEachInitialSourceItemAtItsAuthoredPosition()
        {
            ProcessCreationResult result = CreateTutorialTwo();
            SourceArrangement arrangement = result.State.InitialArrangement;

            Assert.AreEqual(SourceSlotKind.Instruction, arrangement.Slots[2].Kind, "position 3 holds the first item.");
            Assert.AreEqual(SourceSlotKind.Instruction, arrangement.Slots[3].Kind);
            Assert.AreEqual(SourceSlotKind.Instruction, arrangement.Slots[4].Kind);
            Assert.AreEqual(SourceSlotKind.Empty, arrangement.Slots[5].Kind, "position 6 was never filled.");
        }

        [Test]
        public void Create_ResolvesADuplicateDefinitionToSuffixOneThenTwo()
        {
            ProcessCreationResult result = CreateTutorialTwo();
            SourceArrangement arrangement = result.State.InitialArrangement;

            InstanceID first = arrangement.Slots[2].Instruction.InstanceID;
            InstanceID second = arrangement.Slots[3].Instruction.InstanceID;

            Assert.AreNotEqual(first, second, "two copies of one definition are two instances.");
            Assert.Less(first.Value, second.Value, "the lower suffix is taken first.");
        }

        [Test]
        public void Create_MarksEveryInstanceItDrewOnExposed()
        {
            ProcessCreationResult result = CreateTutorialTwo();

            Assert.AreEqual(
                4,
                result.State.ExposedInstanceIDs.Count,
                "three pre-installed source items plus the one scripted starting Buffer item."
            );
        }

        [Test]
        public void Create_PutsTheScriptedInitialBufferItemInSlotOne()
        {
            ProcessCreationResult result = CreateTutorialTwo();

            Assert.IsNotNull(result.State.Buffer.Slots[0].Item);
            Assert.AreEqual(
                ProgressionFixtures.ValuePlusThree,
                result.State.Buffer.Slots[0].Item.DefinitionID
            );
        }

        [Test]
        public void Create_WithAnIllegalInitialPosition_IsRejected()
        {
            ProcessCreationResult result = Create(
                TutorialTwoConfiguration(initialSource: new InitialSourceSpec[]
                {
                    new InitialSourceSpec(1, ProgressionFixtures.ValuePlusTwo)
                })
            );

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ProcessCreationRejection.InitialSourceIllegal, result.Rejection);
        }

        [Test]
        public void Create_WithInitialSourceContentTheRepositoryLacks_IsRejected()
        {
            ProcessCreationResult result = Create(
                TutorialTwoConfiguration(initialSource: new InitialSourceSpec[]
                {
                    new InitialSourceSpec(3, "WB-INS-404")
                })
            );

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ProcessCreationRejection.ScriptedContentMissing, result.Rejection);
        }

        [Test]
        public void Create_WithACoreCarryingAFixedStructure_IsRejectedByName()
        {
            ProcessCreationResult result = Create(
                TutorialTwoConfiguration(core: "WB-CORE-003"),
                CatalogWithStructureCore()
            );

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ProcessCreationRejection.CoreStructureUnsupported, result.Rejection);
        }

        [Test]
        public void Create_WithADrawnBufferLoadAndNoConfirmedBranch_IsRejectedByName()
        {
            ProcessCreationResult result = Create(TutorialTwoConfiguration(drawn: true));

            Assert.IsFalse(result.Succeeded, "a drawn load has nothing to draw from without a Branch.");
            Assert.AreEqual(ProcessCreationRejection.BranchMissing, result.Rejection);
        }

        [Test]
        public void Arrive_FiresEachAuthoredMomentOnceInOrder()
        {
            ProcessState process = CreateTutorialTwo().State;

            ArrivalResult first = process.Arrive(new ArrivalMoment(1));

            Assert.IsTrue(first.Succeeded);
            Assert.AreEqual(2, first.Arrived.Count, "the after-one moment loads two items.");
            Assert.AreEqual(ProgressionFixtures.RepeatTwo, first.Arrived[1].DefinitionID, "in authored order.");
        }

        [Test]
        public void Arrive_TheSameMomentTwice_IsRejected()
        {
            ProcessState process = CreateTutorialTwo().State;
            process.Arrive(new ArrivalMoment(1));

            ArrivalResult again = process.Arrive(new ArrivalMoment(1));

            Assert.IsFalse(again.Succeeded);
            Assert.AreEqual(ArrivalRejection.MomentAlreadyFired, again.Rejection);
        }

        [Test]
        public void Arrive_AMomentTheConfigurationDoesNotDeclare_IsRejected()
        {
            ProcessConfigurationDefinition configuration = TutorialTwoConfiguration(
                arrivals: new ArrivalLoadSpec[]
                {
                    new ArrivalLoadSpec(1, new[] { ProgressionFixtures.RepeatTwo }),
                    new ArrivalLoadSpec(3, new[] { ProgressionFixtures.Overclock })
                }
            );

            ProcessState process = Create(configuration).State;

            ArrivalResult result = process.Arrive(new ArrivalMoment(2));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(
                ArrivalRejection.NoSuchMoment,
                result.Rejection,
                "moment two is undeclared but still inside the allowance, so it is not the after-final rejection."
            );
        }

        [Test]
        public void Arrive_NeverFiresAfterTheFinalExecution()
        {
            ProcessConfigurationDefinition configuration = TutorialTwoConfiguration(
                arrivals: new ArrivalLoadSpec[]
                {
                    new ArrivalLoadSpec(4, new[] { ProgressionFixtures.ValuePlusThree })
                }
            );

            ProcessCreationResult created = Create(configuration);
            ArrivalResult result = created.State.Arrive(new ArrivalMoment(4));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ArrivalRejection.AfterFinalExecution, result.Rejection);
        }

        [Test]
        public void Arrive_WithNoInstallsBetweenMoments_OverflowsAsSoonAsTheBufferFills()
        {
            ProcessState process = CreateTutorialTwo().State;

            process.Arrive(new ArrivalMoment(1));
            ArrivalResult second = process.Arrive(new ArrivalMoment(2));

            Assert.IsTrue(second.Succeeded, "a moment still fires when the Buffer is full; the item is held.");
            Assert.IsTrue(process.Buffer.IsOverflowing);
            Assert.AreEqual(
                ProgressionFixtures.ValuePlusTwo,
                process.Buffer.IncomingItem.DefinitionID,
                "one starting item plus the after-one pair already fill three slots."
            );
        }

        [Test]
        public void Arrive_WithInstallsBetweenMoments_OverflowsOnTheAfterThreePairWithTheDirectiveFirst()
        {
            ProcessState process = CreateTutorialTwo().State;

            Install(process, process.Buffer.Slots[0].Item.InstanceID);
            process.Arrive(new ArrivalMoment(1));
            Install(process, process.Buffer.Slots[0].Item.InstanceID);
            process.Arrive(new ArrivalMoment(2));

            Assert.IsFalse(process.Buffer.IsOverflowing, "installing between moments keeps the Buffer ahead.");

            ArrivalResult third = process.Arrive(new ArrivalMoment(3));

            Assert.IsTrue(third.Succeeded);
            Assert.IsTrue(process.Buffer.IsOverflowing, "the after-three pair is the one that overflows.");
            Assert.AreEqual(
                ProgressionFixtures.ValuePlusThree,
                process.Buffer.IncomingItem.DefinitionID,
                "OVERCLOCK entered first, so the Instruction behind it is the held item."
            );

            Assert.AreEqual(BufferItemState.Present, process.Buffer.StateOf(third.Arrived[0].InstanceID));
        }

        [Test]
        public void Arrive_ResolvesContentTheInitialSourceAlreadyTook()
        {
            ProcessState process = CreateTutorialTwo().State;
            List<InstanceID> exposedBefore = new(process.ExposedInstanceIDs);

            ArrivalResult result = process.Arrive(new ArrivalMoment(2));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, result.Arrived.Count);
            Assert.AreEqual(
                ProgressionFixtures.ValuePlusTwo,
                result.Arrived[0].DefinitionID,
                "the moment names content the initial source already took copies of."
            );

            foreach (InstanceID exposed in exposedBefore)
            {
                Assert.AreNotEqual(
                    exposed,
                    result.Arrived[0].InstanceID,
                    "the arrival resolved a fresh instance, not one already spoken for."
                );
            }

            Assert.Contains(
                result.Arrived[0].InstanceID,
                new List<InstanceID>(process.ExposedInstanceIDs),
                "and the arrival marks its own instance exposed in turn."
            );
        }

        [Test]
        public void Arrive_WithContentTheRepositoryLacks_IsRejected()
        {
            ProcessConfigurationDefinition configuration = TutorialTwoConfiguration(
                arrivals: new ArrivalLoadSpec[]
                {
                    new ArrivalLoadSpec(1, new[] { "WB-INS-404" })
                }
            );

            ProcessState process = Create(configuration).State;
            ArrivalResult result = process.Arrive(new ArrivalMoment(1));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ArrivalRejection.ContentMissing, result.Rejection);
        }

        [Test]
        public void ProcessRule_IsInstantiatedWhenTheConfigurationNamesOne()
        {
            ProcessState process = CreateTutorialTwo(withProcessRule: true).State;

            Assert.IsNotNull(process.ProcessRule);
            Assert.AreEqual("WB-PRC-002", process.ProcessRule.Definition.ID.Value);
        }

        [Test]
        public void ProcessRule_IsNullWhenTheConfigurationNamesNone()
        {
            ProcessState process = CreateTutorialTwo().State;

            Assert.IsNull(process.ProcessRule);
            Assert.AreEqual(0, process.CompilationEffects.Count);
        }

        [Test]
        public void CompilationEffects_CarryEveryCompilationDomainEffectOfTheRule()
        {
            ProcessState process = CreateTutorialTwo(withProcessRule: true).State;

            Assert.AreEqual(1, process.CompilationEffects.Count);
            Assert.AreEqual("TUTORIAL FREE COMPILATION", process.CompilationEffects[0].SourceDisplayName);
        }

        [Test]
        public void CompilationEffects_MakeAnEditedCompilationCostNothing()
        {
            ProcessState process = CreateTutorialTwo(withProcessRule: true).State;
            BuildState build = new(process.InitialArrangement, process.Buffer, ProgressionFixtures.Parameters());
            build.Compile(process.Bytes.Balance, process.CompilationEffects);
            build.Apply(new RemoveEdit(new SourcePosition(3)));

            CompilationAttempt attempt = build.Compile(process.Bytes.Balance, process.CompilationEffects);

            Assert.AreEqual(
                CompilationClassification.OrdinaryEdited,
                attempt.Breakdown.Classification,
                "the initial build is compiled first, so this one is genuinely an edit."
            );

            Assert.AreEqual(0, attempt.Breakdown.FinalCost, "the Process rule sets an edited compilation to zero Bytes.");
        }

        [Test]
        public void WithoutTheProcessRule_AnEditedCompilationStillCosts()
        {
            ProcessState process = CreateTutorialTwo().State;
            BuildState build = new(process.InitialArrangement, process.Buffer, ProgressionFixtures.Parameters());
            build.Compile(process.Bytes.Balance, process.CompilationEffects);
            build.Apply(new RemoveEdit(new SourcePosition(3)));

            CompilationAttempt attempt = build.Compile(process.Bytes.Balance, process.CompilationEffects);

            Assert.AreEqual(CompilationClassification.OrdinaryEdited, attempt.Breakdown.Classification);
            Assert.Greater(attempt.Breakdown.FinalCost, 0, "without the rule the first edited compilation costs its band.");
        }

        [Test]
        public void Snapshot_CarriesTheProcessShape()
        {
            ProcessState process = CreateTutorialTwo().State;

            ProcessSnapshot snapshot = process.Snapshot();

            Assert.AreEqual(3, snapshot.Bytes.Value);
            Assert.AreEqual(0, snapshot.ExecutionsRun);
            Assert.AreEqual(3, snapshot.BufferSlots.Count);
        }

        /// <summary>
        /// Simulates the player installing a buffered item, which is the Take half of Compilation's
        /// install edit — the same call <c>InstallEdit</c> makes.
        /// </summary>
        /// <param name="process">The Process whose Buffer is being drawn from.</param>
        /// <param name="id">The buffered instance to install.</param>
        private static void Install(ProcessState process, InstanceID id)
        {
            process.Buffer.Take(id);
        }

        /// <summary>
        /// Creates the Tutorial Process 2 fixture, optionally carrying the free-compilation Process rule.
        /// </summary>
        /// <param name="withProcessRule">Whether the configuration names a Process rule.</param>
        /// <returns>The creation result.</returns>
        private static ProcessCreationResult CreateTutorialTwo(bool withProcessRule = false)
        {
            return Create(TutorialTwoConfiguration(withProcessRule: withProcessRule));
        }

        [Test]
        public void Archive_WithGarbageCollectorInstalled_CreditsOneByteOnce()
        {
            // Tutorial 2's scripted Buffer load seeds exactly one item, so the second archive needs
            // an arrival to have anything to archive.
            ProcessState process = WithGarbageCollector(out SessionState _);
            InstanceID first = process.Buffer.Slots[0].Item.InstanceID;
            ByteAmount before = process.Bytes.Balance;

            process.Archive(first);
            ByteAmount afterFirst = process.Bytes.Balance;
            process.Arrive(new ArrivalMoment(1));
            process.Archive(process.Buffer.Slots[0].Item.InstanceID);

            Assert.AreEqual(before.Value + 1, afterFirst.Value);
            Assert.AreEqual(afterFirst.Value, process.Bytes.Balance.Value);
            Assert.AreEqual(1, process.ResourceGains.Count);
            Assert.AreEqual("BYTES", process.ResourceGains[0].Resource);
            Assert.AreEqual(first, process.ResourceGains[0].ArchivedInstance);
        }

        [Test]
        public void Archive_WithoutGarbageCollector_CreditsNothing()
        {
            ProcessState process = CreateTutorialTwo().State;
            ByteAmount before = process.Bytes.Balance;

            process.Archive(process.Buffer.Slots[0].Item.InstanceID);

            Assert.AreEqual(before, process.Bytes.Balance);
            Assert.AreEqual(0, process.ResourceGains.Count);
            Assert.AreEqual(0, process.ArchiveObservers.Count);
        }

        [Test]
        public void Archive_ARejectedArchive_CreditsNothing()
        {
            ProcessState process = WithGarbageCollector(out SessionState _);
            ByteAmount before = process.Bytes.Balance;

            ArchiveResult result = process.Archive(new InstanceID(9999));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(before, process.Bytes.Balance);
            Assert.AreEqual(0, process.ResourceGains.Count);
        }

        [Test]
        public void Archive_ANewProcessForTheSameSession_FiresAgain()
        {
            ProcessState first = WithGarbageCollector(out SessionState session);
            first.Archive(first.Buffer.Slots[0].Item.InstanceID);
            ProcessState second = CreateOver(session).State;
            ByteAmount before = second.Bytes.Balance;

            second.Archive(second.Buffer.Slots[0].Item.InstanceID);

            Assert.AreEqual(before.Value + 1, second.Bytes.Balance.Value);
            Assert.AreEqual(1, second.ResourceGains.Count);
        }

        [Test]
        public void Take_ConsumingADirective_CreditsNothing()
        {
            ProcessState process = WithGarbageCollector(out SessionState _);
            ByteAmount before = process.Bytes.Balance;

            process.Buffer.Take(process.Buffer.Slots[0].Item.InstanceID);

            Assert.AreEqual(before, process.Bytes.Balance);
            Assert.AreEqual(0, process.ResourceGains.Count);
        }

        [Test]
        public void ArchiveObservers_AreBuiltFromTheInstalledDependencies()
        {
            ProcessState process = WithGarbageCollector(out SessionState _);

            Assert.AreEqual(1, process.ArchiveObservers.Count);
            Assert.AreEqual("WB-DEP-008", process.ArchiveObservers[0].DefinitionID);
        }

        /// <summary>
        /// Creates a Tutorial 2 Process over a Session with GARBAGE COLLECTOR installed.
        /// </summary>
        /// <param name="session">The Session the Process was created over.</param>
        /// <returns>The Process state.</returns>
        private static ProcessState WithGarbageCollector(out SessionState session)
        {
            ContentCatalog catalog = ProgressionFixtures.ProcessCatalog();
            session = SessionState.Create(
                catalog,
                ProgressionFixtures.Archetype(
                    ProgressionFixtures.ValuePlusTwo,
                    ProgressionFixtures.ValuePlusTwo,
                    ProgressionFixtures.ValuePlusTwo,
                    ProgressionFixtures.ScorePlusValue,
                    ProgressionFixtures.ValuePlusThree,
                    ProgressionFixtures.ValuePlusThree,
                    ProgressionFixtures.ValuePlusThree,
                    ProgressionFixtures.RepeatTwo,
                    ProgressionFixtures.Overclock
                ),
                "WB-SYS-001",
                "session-1",
                "seed-1"
            );

            session.Economy.Dependencies.Install(
                EconomyFixtures.GarbageCollector(),
                session.InstanceIDs.Next(),
                0,
                DependencyOrigin.Purchase);

            return CreateOver(session).State;
        }

        /// <summary>
        /// Creates a Tutorial 2 Process over an existing Session.
        /// </summary>
        /// <param name="session">The Session to create over.</param>
        /// <returns>The creation result.</returns>
        private static ProcessCreationResult CreateOver(SessionState session)
        {
            ContentCatalog catalog = ProgressionFixtures.ProcessCatalog();
            ProcessConfigurationDefinition configuration = TutorialTwoConfiguration();
            ProcessSetup setup = ProcessSetupResolver.Resolve(
                configuration,
                catalog.Parameters,
                Array.Empty<ActiveSetupEffect>()
            );

            return ProcessState.Create(configuration, setup, catalog, session, null);
        }


        /// <summary>
        /// Creates a Process from a configuration against the fixture catalog and a seeded Session.
        /// </summary>
        /// <param name="configuration">The Process configuration.</param>
        /// <param name="catalog">The catalog to resolve the Core and rule through.</param>
        /// <returns>The creation result.</returns>
        private static ProcessCreationResult Create(
            ProcessConfigurationDefinition configuration,
            ContentCatalog catalog = null
        )
        {
            ContentCatalog resolved = catalog ?? ProgressionFixtures.ProcessCatalog();
            SessionState session = SessionState.Create(
                resolved,
                ProgressionFixtures.Archetype(
                    ProgressionFixtures.ValuePlusTwo,
                    ProgressionFixtures.ValuePlusTwo,
                    ProgressionFixtures.ValuePlusTwo,
                    ProgressionFixtures.ScorePlusValue,
                    ProgressionFixtures.ValuePlusThree,
                    ProgressionFixtures.ValuePlusThree,
                    ProgressionFixtures.ValuePlusThree,
                    ProgressionFixtures.RepeatTwo,
                    ProgressionFixtures.Overclock
                ),
                "WB-SYS-001",
                "session-1",
                "seed-1"
            );

            ProcessSetup setup = ProcessSetupResolver.Resolve(
                configuration,
                resolved.Parameters,
                Array.Empty<ActiveSetupEffect>()
            );

            return ProcessState.Create(configuration, setup, resolved, session, null);
        }

        /// <summary>
        /// Builds the Tutorial Process 2 arrangement by hand, to compare against the one Compilation
        /// produced: an eight-slot Core with three installed Instructions at positions three to five.
        /// </summary>
        /// <returns>The hand-built arrangement.</returns>
        private static SourceArrangement BuildByHand()
        {
            List<SourceSlot> slots = new()
            {
                SourceSlot.ForCore(new SourcePosition(1), CoreLineAt(1)),
                SourceSlot.ForCore(new SourcePosition(2), CoreLineAt(2)),
                SourceSlot.ForInstruction(new SourcePosition(3), Instance(1)),
                SourceSlot.ForInstruction(new SourcePosition(4), Instance(2)),
                SourceSlot.ForInstruction(new SourcePosition(5), Instance(3)),
                SourceSlot.ForEmpty(new SourcePosition(6)),
                SourceSlot.ForEmpty(new SourcePosition(7)),
                SourceSlot.ForCore(new SourcePosition(8), CoreLineAt(8))
            };

            return new SourceArrangement(slots);
        }

        /// <summary>
        /// Builds a Core line for the hand-built comparison arrangement.
        /// </summary>
        /// <param name="position">The one-based Core position.</param>
        /// <returns>The Core line.</returns>
        private static CoreLine CoreLineAt(int position)
        {
            return new CoreLine(
                "WB-CORE-002:L" + position.ToString("00"),
                new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1))
            );
        }

        /// <summary>
        /// Builds an Instruction instance for the hand-built comparison arrangement.
        /// </summary>
        /// <param name="id">The instance identity number.</param>
        /// <returns>The Instruction instance.</returns>
        private static InstructionInstance Instance(int id)
        {
            return new InstructionInstance(
                new InstanceID(id),
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                Array.Empty<PatchAttachment>()
            );
        }

        /// <summary>
        /// Builds a catalog whose only Core carries a fixed Structure.
        /// </summary>
        /// <returns>The catalog.</returns>
        private static ContentCatalog CatalogWithStructureCore()
        {
            return ProgressionFixtures.ProcessCatalog(structureCore: true);
        }

        /// <summary>
        /// Builds the Tutorial Process 2 configuration, with the pieces each test varies.
        /// </summary>
        /// <param name="initialSource">The pre-installed source entries.</param>
        /// <param name="arrivals">The scripted arrival moments.</param>
        /// <param name="core">The Core identity.</param>
        /// <param name="drawn">Whether the Buffer load is drawn rather than scripted.</param>
        /// <param name="withProcessRule">Whether the configuration names a Process rule.</param>
        /// <returns>The configuration.</returns>
        private static ProcessConfigurationDefinition TutorialTwoConfiguration(
            IReadOnlyList<InitialSourceSpec> initialSource = null,
            IReadOnlyList<ArrivalLoadSpec> arrivals = null,
            string core = "WB-CORE-002",
            bool drawn = false,
            bool withProcessRule = false
        )
        {
            BufferLoadSpec load = drawn
                ? new BufferLoadSpec(BufferLoadPolicy.Drawn, Array.Empty<string>(), Array.Empty<ArrivalLoadSpec>(), 3, new[] { 1, 2, 3 })
                : new BufferLoadSpec(
                    BufferLoadPolicy.Scripted,
                    new[] { ProgressionFixtures.ValuePlusThree },
                    arrivals ?? DefaultArrivals(),
                    0,
                    Array.Empty<int>());

            return new ProcessConfigurationDefinition(
                new ProcessID("WB-PROC-002"),
                "Tutorial Process 2",
                ProcessRole.Tutorial2,
                new CoreID(core),
                withProcessRule ? new ProcessRuleID("WB-PRC-002") : null,
                new ProcessThresholdSpec(18, 32, 80),
                4,
                true,
                3,
                3,
                5,
                initialSource ?? DefaultInitialSource(),
                load,
                null,
                null,
                new RewardPackageID("WB-RWD-002"),
                null
            );
        }

        /// <summary>
        /// The fixture's pre-installed source: two copies of one Instruction and a scoring Instruction.
        /// </summary>
        /// <returns>The initial-source entries.</returns>
        private static IReadOnlyList<InitialSourceSpec> DefaultInitialSource()
        {
            return new InitialSourceSpec[]
            {
                new InitialSourceSpec(3, ProgressionFixtures.ValuePlusTwo),
                new InitialSourceSpec(4, ProgressionFixtures.ValuePlusTwo),
                new InitialSourceSpec(5, ProgressionFixtures.ScorePlusValue)
            };
        }

        /// <summary>
        /// The fixture's scripted arrival moments, ending with the pair that overflows a three-slot
        /// Buffer with the Directive entering first.
        /// </summary>
        /// <returns>The arrival moments.</returns>
        private static IReadOnlyList<ArrivalLoadSpec> DefaultArrivals()
        {
            return new ArrivalLoadSpec[]
            {
                new ArrivalLoadSpec(1, new[] { ProgressionFixtures.ValuePlusThree, ProgressionFixtures.RepeatTwo }),
                new ArrivalLoadSpec(2, new[] { ProgressionFixtures.ValuePlusTwo }),
                new ArrivalLoadSpec(3, new[] { ProgressionFixtures.Overclock, ProgressionFixtures.ValuePlusThree })
            };
        }
    }
}
