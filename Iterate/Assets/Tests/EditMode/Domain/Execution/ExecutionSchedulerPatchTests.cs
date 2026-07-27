using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the six Patch effects end to end on fixture arrangements: CONSTANT PATCH's persistent
    /// operand adjustment and its composition order ahead of a selected-host modification, FEEDBACK
    /// PATCH's host-linked Score reaction and its stable evidence order, ECHO PATCH's per-source-
    /// execution even-line descendant, TERMINAL PATCH's final-occupied-player-line fact computed
    /// beside the boundary index, TRUE PATCH's retained Condition outcome on canonical and
    /// re-entered closures, PIPELINE PATCH's runtime adjacency, and the host-socketed commit
    /// evidence — each with hand-traced registers stated in the Arrange comment.
    /// </summary>
    public sealed class ExecutionSchedulerPatchTests
    {
        [Test]
        public void ConstantPatch_FixedAdditionHost_LandsBasePlusOne()
        {
            // Value += 2 with the +1 persistent adjustment lands 3.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), PatchedHost(10, 60, "WB-PAT-001", ConstantPatchEffect(), ValueAddOperation(2)))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids));

            Assert.AreEqual(new ValueAmount(3), record.FinalState.FinalValue);
            List<EventEvidence> events = UnitEvents(record, record.Units[0]);
            int modified = IndexOfSubtype(events, ExecutionEventSubtypes.PrimaryOperationModified);
            Assert.AreNotEqual(-1, modified, "the persistent adjustment modifies the pending operation");
            Assert.AreEqual(new InstanceID(60), events[modified].EffectOriginInstance);
        }

        [Test]
        public void ConstantPatch_WithStandardLibraryShape_OrdersPersistentBeforeSelectedHost()
        {
            // Authored 2 → persistent Patch +1 → selected-host +1: Value lands 4, Patch evidence first.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), PatchedHost(10, 60, "WB-PAT-001", ConstantPatchEffect(), ValueAddOperation(2)))
            });
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                SchedulerFixtures.StandardLibraryInstance(40)
            };

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids, installed));

            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
            List<EventEvidence> events = UnitEvents(record, record.Units[0]);
            List<int> modifiedIndexes = IndexesOfSubtype(events, ExecutionEventSubtypes.PrimaryOperationModified);
            Assert.AreEqual(2, modifiedIndexes.Count);
            Assert.AreEqual(new InstanceID(60), events[modifiedIndexes[0]].EffectOriginInstance, "the persistent adjustment applies first");
            Assert.AreEqual(new InstanceID(40), events[modifiedIndexes[1]].EffectOriginInstance, "the selected-host modification applies second");
        }

        [Test]
        public void FeedbackPatch_OwnHostScoreGain_AddsValueAtReactionDepth()
        {
            // Score += 3 fires the host-linked reaction: Value 0 → 1 at causal depth 2.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), PatchedHost(10, 60, "WB-PAT-005", FeedbackPatchEffect(), ScoreAddOperation(3)))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids));

            Assert.AreEqual(new ScoreValue(3), record.FinalState.FinalScore);
            Assert.AreEqual(new ValueAmount(1), record.FinalState.FinalValue);

            List<EventEvidence> events = UnitEvents(record, record.Units[0]);
            int reactionQuantity = IndexOfReactionQuantity(events, new InstanceID(60));
            Assert.AreNotEqual(-1, reactionQuantity);
            Assert.AreEqual(2, events[reactionQuantity].CausalDepth);
            Assert.AreEqual(record.Units[0].Identity, events[reactionQuantity].ContainingUnit);
        }

        [Test]
        public void FeedbackPatch_ForeignScoreGain_IsStructurallySilent()
        {
            // The unpatched unit's Score gain is foreign: no near-miss, no fire; the own host's Value
            // gain near-misses the register requirement once.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), UnpatchedHost(20, ScoreAddOperation(3))),
                SourceSlot.ForInstruction(new SourcePosition(2), PatchedHost(10, 60, "WB-PAT-005", FeedbackPatchEffect(), ValueAddOperation(1)))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids));

            Assert.AreEqual(new ValueAmount(1), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(3), record.FinalState.FinalScore);
            Assert.AreEqual(0, CountChainEvents(record, ExecutionEventSubtypes.EffectCommitted, new InstanceID(60)));

            List<EventEvidence> nearMisses = NearMissesOf(record, new InstanceID(60));
            Assert.AreEqual(1, nearMisses.Count, "one near-miss on the own host's event, silence on the foreign one");
            Assert.AreEqual("REGISTER:SCORE", nearMisses[0].DispositionReason);
            Assert.AreEqual(record.Units[1].Identity, nearMisses[0].ContainingUnit);
        }

        [Test]
        public void FeedbackPatch_BeforeOutputCacheShape_OnOneScoreEvent()
        {
            // Both react to Score += 3; FEEDBACK PATCH resolves first by rank though its instance
            // identity is higher: Value 0 → 2.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), PatchedHost(10, 60, "WB-PAT-005", FeedbackPatchEffect(), ScoreAddOperation(3)))
            });
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                OutputCacheInstance(40)
            };

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids, installed));

            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
            List<EventEvidence> events = UnitEvents(record, record.Units[0]);
            int feedback = IndexOfReactionQuantity(events, new InstanceID(60));
            int outputCache = IndexOfReactionQuantity(events, new InstanceID(40));
            Assert.AreNotEqual(-1, feedback);
            Assert.AreNotEqual(-1, outputCache);
            Assert.Less(feedback, outputCache, "the host-local Patch reaction resolves before OUTPUT CACHE");
        }

        [Test]
        public void EchoPatch_EvenLineHost_FiresOneDescendant()
        {
            // Core assigns 1; the even-line host adds 2 canonically and 2 in its descendant: Value 5.
            ExecutionRecord record = Execute(EchoEvenLineRequest());

            Assert.AreEqual(3, record.Units.Count);
            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(new ValueAmount(5), record.FinalState.FinalValue);
            Assert.AreEqual(ActivationKind.AddedExecution, record.Units[2].Opening.Activation);
        }

        [Test]
        public void EchoPatch_OwnDescendantClosure_IsOriginLocked()
        {
            // The descendant's closure is eligible under its own source-execution key; only the
            // origin lock stops a second request.
            ExecutionRecord record = Execute(EchoEvenLineRequest());

            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(1, CountNearMissReason(record, "ORIGIN_LOCK:" + new InstanceID(60)));
        }

        [Test]
        public void EchoPatch_InsideRepeatTwo_FiresPerIteration()
        {
            // Once per source execution of the host: each iteration's unit fires its own descendant —
            // Value climbs 1, 2, 3, 4 across two iterations and two descendants.
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance repeat = SchedulerFixtures.RepeatStructure(20, 2, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), repeat),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), repeat, PatchedHost(10, 60, "WB-PAT-002", EchoPatchEffect(), ValueAddOperation(1)))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids));

            Assert.AreEqual(4, record.Units.Count);
            Assert.AreEqual(2, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
        }

        [Test]
        public void EchoPatch_OddLineHost_NearMissesEvenLine()
        {
            // Position 1 is odd: one named near-miss, no request.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), PatchedHost(10, 60, "WB-PAT-002", EchoPatchEffect(), ValueAddOperation(1)))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids));

            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(1, CountNearMissReason(record, "POSITIONAL:EVEN_NUMBERED_LINE"));
            Assert.AreEqual(new ValueAmount(1), record.FinalState.FinalValue);
        }

        [Test]
        public void EchoPatch_CommitEvidence_CarriesTheHostSocketedOrigin()
        {
            // The Patch instance is the effect origin and the host instance rides the host field on
            // the qualification and commitment chain.
            ExecutionRecord record = Execute(EchoEvenLineRequest());

            List<EventEvidence> qualified = ChainEventsOf(record, ExecutionEventSubtypes.EffectQualified, new InstanceID(60));
            List<EventEvidence> committed = ChainEventsOf(record, ExecutionEventSubtypes.EffectCommitted, new InstanceID(60));
            Assert.AreEqual(1, qualified.Count);
            Assert.AreEqual(1, committed.Count);
            Assert.AreEqual(new InstanceID(10), qualified[0].HostInstance);
            Assert.AreEqual(new InstanceID(10), committed[0].HostInstance);
        }

        [Test]
        public void TerminalPatch_FinalOccupiedPlayerLineWithEmptyBelow_Fires()
        {
            // The position-2 host is the final occupied player line; the empty at 3 does not block.
            // Value 1 + 2 + 2 = 5; the trailing Core line reads it into Score.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), UnpatchedHost(20, ValueAddOperation(1))),
                SourceSlot.ForInstruction(new SourcePosition(2), PatchedHost(10, 60, "WB-PAT-003", TerminalPatchEffect(), ValueAddOperation(2))),
                SourceSlot.ForEmpty(new SourcePosition(3)),
                SourceSlot.ForCore(new SourcePosition(4), ScoreReadsValueCoreLine("core-04"))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids));

            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(new ValueAmount(5), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(5), record.FinalState.FinalScore);
        }

        [Test]
        public void TerminalPatch_TwoPatchedHosts_OnlyTheFinalLineFires()
        {
            // Hosts at 1 and 2 both carry TERMINAL-shaped Patches; only position 2 is the final
            // occupied player line — position 1 near-misses by name.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), PatchedHost(11, 61, "WB-PAT-003", TerminalPatchEffect(), ValueAddOperation(1))),
                SourceSlot.ForInstruction(new SourcePosition(2), PatchedHost(10, 60, "WB-PAT-003", TerminalPatchEffect(), ValueAddOperation(2))),
                SourceSlot.ForCore(new SourcePosition(3), ScoreReadsValueCoreLine("core-03"))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids));

            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(new InstanceID(60), FindGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested).EffectOriginInstance);

            List<EventEvidence> nearMisses = NearMissesOf(record, new InstanceID(61));
            Assert.AreEqual(1, nearMisses.Count);
            Assert.AreEqual("POSITIONAL:FINAL_OCCUPIED_PLAYER_LINE", nearMisses[0].DispositionReason);
            Assert.AreEqual(new ValueAmount(5), record.FinalState.FinalValue);
        }

        [Test]
        public void TerminalPatch_OccupiedContainedLineBelow_OutranksAnEarlierHost()
        {
            // The occupied contained line at position 3 is the final occupied player line, so the
            // position-1 host near-misses — the fact sees contained occupancy the boundary index
            // stands over.
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance repeat = SchedulerFixtures.RepeatStructure(20, 1, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), PatchedHost(10, 60, "WB-PAT-003", TerminalPatchEffect(), ValueAddOperation(1))),
                SourceSlot.ForStructureHeader(new SourcePosition(2), repeat),
                SourceSlot.ForContainedInstruction(new SourcePosition(3), repeat, UnpatchedHost(21, ValueAddOperation(1)))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids));

            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            List<EventEvidence> nearMisses = NearMissesOf(record, new InstanceID(60));
            Assert.AreEqual(1, nearMisses.Count);
            Assert.AreEqual("POSITIONAL:FINAL_OCCUPIED_PLAYER_LINE", nearMisses[0].DispositionReason);
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
        }

        [Test]
        public void TruePatch_HostInsideSucceedingCondition_FiresWithRetainedIdentity()
        {
            // Value 1 satisfies the predicate; the governed host scores 2 canonically and 2 in its
            // descendant, which retains the evaluation identity.
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance condition = SchedulerFixtures.ConditionStructure(20, CoreRegister.Value, PredicateComparison.AtLeast, 1, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignValueCoreLine("core-01", 1)),
                SourceSlot.ForStructureHeader(new SourcePosition(2), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(3), condition, PatchedHost(10, 60, "WB-PAT-004", TruePatchEffect(), ScoreAddOperation(2)))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids));

            Assert.AreEqual(3, record.Units.Count);
            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(new ScoreValue(4), record.FinalState.FinalScore);
            Assert.AreEqual(
                record.Units[1].Opening.StructureContext.ConditionEvaluationIdentity,
                record.Units[2].Opening.StructureContext.ConditionEvaluationIdentity,
                "the descendant retains the Condition-evaluation identity");
        }

        [Test]
        public void TruePatch_RescuedFromFalseHost_NearMissesByName()
        {
            // The predicate fails against the reset registers; the rescued host resolves but carries
            // the retained FALSE outcome — a named near-miss, never a fire.
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance condition = SchedulerFixtures.ConditionStructure(20, CoreRegister.Value, PredicateComparison.AtLeast, 3, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), condition, PatchedHost(10, 60, "WB-PAT-004", TruePatchEffect(), ValueAddOperation(2)))
            });
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                SchedulerFixtures.SafeModeInstance(40)
            };

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids, installed));

            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            List<EventEvidence> nearMisses = NearMissesOf(record, new InstanceID(60));
            Assert.AreEqual(1, nearMisses.Count);
            Assert.AreEqual("STRUCTURE_CONTEXT:INSIDE_SUCCEEDING_CONDITION", nearMisses[0].DispositionReason);
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
        }

        [Test]
        public void TruePatch_ForeignDescendantOfSucceedingHost_RetainsTrue()
        {
            // The OVERCLOCK-shaped creator re-executes the governed host; the re-entered closure
            // retains TRUE, so the Patch fires from the foreign branch too. Value walks
            // 1 → 3 → 5 → 7 → 9 across the host, its duplicate, the nested descendant, and the
            // Patch's own descendant.
            ExecutionRecord record = Execute(TruePatchWithOverclockRequest(1));

            Assert.AreEqual(5, record.Units.Count);
            Assert.AreEqual(3, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(2, CountRequestsOf(record, new InstanceID(60)), "the Patch fires from its own closure and the re-entered one");
            Assert.AreEqual(new ValueAmount(9), record.FinalState.FinalValue);
        }

        [Test]
        public void TruePatch_ForeignDescendantOfRescuedHost_RetainsFalse()
        {
            // The rescued-from-FALSE host is duplicated by the OVERCLOCK-shaped creator; the
            // re-entered closure retains FALSE and near-misses — the outcome is carried, never
            // derived from the evaluation identity's presence.
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance condition = SchedulerFixtures.ConditionStructure(20, CoreRegister.Value, PredicateComparison.AtLeast, 3, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), condition, PatchedHost(10, 60, "WB-PAT-004", TruePatchEffect(), ValueAddOperation(2)))
            });
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                SchedulerFixtures.SafeModeInstance(40)
            };
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>
            {
                SchedulerFixtures.OverclockPragma(900)
            };

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, installed, pragmas));

            Assert.AreEqual(2, record.Units.Count);
            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(0, CountRequestsOf(record, new InstanceID(60)));
            Assert.AreEqual(2, CountNearMissReason(record, "STRUCTURE_CONTEXT:INSIDE_SUCCEEDING_CONDITION"));
            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
        }

        [Test]
        public void PipelinePatch_AfterScoreIncreasingPlayerInstruction_Fires()
        {
            // The host opens immediately after a successful player Score gain: one descendant.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), UnpatchedHost(20, ScoreAddOperation(2))),
                SourceSlot.ForInstruction(new SourcePosition(2), PatchedHost(10, 60, "WB-PAT-006", PipelinePatchEffect(), ValueAddOperation(1)))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids));

            Assert.AreEqual(3, record.Units.Count);
            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(2), record.FinalState.FinalScore);
        }

        [Test]
        public void PipelinePatch_InterveningSuccessfulNonQualifyingUnit_Breaks()
        {
            // The Signal unit replaces the predecessor without a Score gain: the host near-misses.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), UnpatchedHost(20, ScoreAddOperation(2))),
                SourceSlot.ForInstruction(new SourcePosition(2), UnpatchedHost(21, SignalAddOperation(1))),
                SourceSlot.ForInstruction(new SourcePosition(3), PatchedHost(10, 60, "WB-PAT-006", PipelinePatchEffect(), ValueAddOperation(1)))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids));

            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(1, CountNearMissReason(record, "STRUCTURE_CONTEXT:ADJACENT_AFTER_SUCCESSFUL_SCORE"));
            Assert.AreEqual(new ValueAmount(1), record.FinalState.FinalValue);
        }

        [Test]
        public void PipelinePatch_AddedExecutionPredecessor_Fires()
        {
            // The even-line ECHO host's descendant is the immediately preceding completed source
            // execution the host examines — an added execution as the qualifying predecessor.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), UnpatchedHost(30, ValueAddOperation(1))),
                SourceSlot.ForInstruction(new SourcePosition(2), PatchedHost(20, 61, "WB-PAT-002", EchoPatchEffect(), ScoreAddOperation(2))),
                SourceSlot.ForInstruction(new SourcePosition(3), PatchedHost(10, 60, "WB-PAT-006", PipelinePatchEffect(), ValueAddOperation(1)))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids));

            Assert.AreEqual(5, record.Units.Count);
            Assert.AreEqual(2, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(ActivationKind.AddedExecution, record.Units[2].Opening.Activation, "the ECHO descendant precedes the observing host");
            Assert.AreEqual(new SourcePosition(3), record.Units[3].Opening.Position);
            Assert.AreEqual(1, CountRequestsOf(record, new InstanceID(60)));
            Assert.AreEqual(new ValueAmount(3), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(4), record.FinalState.FinalScore);
        }

        [Test]
        public void RichestPatchArrangement_DoubleRun_IsStructurallyEqual()
        {
            ExecutionRecord first = Execute(RichestRequest());
            ExecutionRecord second = Execute(RichestRequest());

            Assert.AreEqual(first.Events.Count, second.Events.Count);
            Assert.AreEqual(first.Units.Count, second.Units.Count);
            Assert.AreEqual(first.FinalState, second.FinalState);
            Assert.AreEqual(first.SafetyCounts, second.SafetyCounts);

            for (int i = 0; i < first.Events.Count; i++)
            {
                Assert.AreEqual(first.Events[i].Evidence, second.Events[i].Evidence, "event " + i);
            }

            for (int i = 0; i < first.Units.Count; i++)
            {
                Assert.AreEqual(first.Units[i].Opening, second.Units[i].Opening, "unit " + i);
            }
        }

        /// <summary>
        /// The even-line ECHO fixture: Core assigns Value 1, the position-2 host adds 2 canonically
        /// and 2 in its descendant.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest EchoEvenLineRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignValueCoreLine("core-01", 1)),
                SourceSlot.ForInstruction(new SourcePosition(2), PatchedHost(10, 60, "WB-PAT-002", EchoPatchEffect(), ValueAddOperation(2)))
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids);
        }

        /// <summary>
        /// The TRUE-PATCH-with-creator fixture: Core assigns Value, the governed host carries the
        /// TRUE-shaped Patch, and the OVERCLOCK-shaped pragma duplicates the host's first Value gain.
        /// </summary>
        /// <param name="coreAssign">The Core line's assigned Value.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest TruePatchWithOverclockRequest(int coreAssign)
        {
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance condition = SchedulerFixtures.ConditionStructure(20, CoreRegister.Value, PredicateComparison.AtLeast, 1, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignValueCoreLine("core-01", coreAssign)),
                SourceSlot.ForStructureHeader(new SourcePosition(2), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(3), condition, PatchedHost(10, 60, "WB-PAT-004", TruePatchEffect(), ValueAddOperation(2)))
            });
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>
            {
                SchedulerFixtures.OverclockPragma(900)
            };

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), pragmas);
        }

        /// <summary>
        /// The richest fixture: CONSTANT, TRUE, PIPELINE, and TERMINAL Patches together with a
        /// trailing Core output over a succeeding Condition.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RichestRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance condition = SchedulerFixtures.ConditionStructure(20, CoreRegister.Value, PredicateComparison.AtLeast, 1, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignValueCoreLine("core-01", 1)),
                SourceSlot.ForInstruction(new SourcePosition(2), PatchedHost(10, 60, "WB-PAT-001", ConstantPatchEffect(), ValueAddOperation(2))),
                SourceSlot.ForStructureHeader(new SourcePosition(3), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(4), condition, PatchedHost(11, 61, "WB-PAT-004", TruePatchEffect(), ScoreAddOperation(2))),
                SourceSlot.ForInstruction(new SourcePosition(5), PatchedHost(12, 62, "WB-PAT-006", PipelinePatchEffect(), ValueAddOperation(1))),
                SourceSlot.ForInstruction(new SourcePosition(6), PatchedHost(13, 63, "WB-PAT-003", TerminalPatchEffect(), ScoreAddOperation(3))),
                SourceSlot.ForEmpty(new SourcePosition(7)),
                SourceSlot.ForCore(new SourcePosition(8), ScoreReadsValueCoreLine("core-08"))
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids);
        }

        /// <summary>
        /// Runs one request through a fresh scheduler.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <returns>The frozen record.</returns>
        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        /// <summary>
        /// A Core line assigning a constant to Value.
        /// </summary>
        /// <param name="identity">The Core line identity.</param>
        /// <param name="constant">The assigned constant.</param>
        /// <returns>The Core line.</returns>
        private static CoreLine AssignValueCoreLine(string identity, int constant)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(constant)));
        }

        /// <summary>
        /// A Core line adding the current Value into Score.
        /// </summary>
        /// <param name="identity">The Core line identity.</param>
        /// <returns>The Core line.</returns>
        private static CoreLine ScoreReadsValueCoreLine(string identity)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));
        }

        /// <summary>
        /// A fixed <c>Value += constant</c> primary operation.
        /// </summary>
        /// <param name="constant">The fixed operand.</param>
        /// <returns>The operation.</returns>
        private static QuantityChangeOperation ValueAddOperation(int constant)
        {
            return new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(constant));
        }

        /// <summary>
        /// A fixed <c>Score += constant</c> primary operation.
        /// </summary>
        /// <param name="constant">The fixed operand.</param>
        /// <returns>The operation.</returns>
        private static QuantityChangeOperation ScoreAddOperation(int constant)
        {
            return new QuantityChangeOperation(CoreRegister.Score, QuantityOperator.Add, OperandSpec.FromConstant(constant));
        }

        /// <summary>
        /// A fixed <c>Signal += constant</c> primary operation.
        /// </summary>
        /// <param name="constant">The fixed operand.</param>
        /// <returns>The operation.</returns>
        private static QuantityChangeOperation SignalAddOperation(int constant)
        {
            return new QuantityChangeOperation(CoreRegister.Signal, QuantityOperator.Add, OperandSpec.FromConstant(constant));
        }

        /// <summary>
        /// An unpatched host Instruction instance with the given primary operation.
        /// </summary>
        /// <param name="hostID">The host's instance identity value.</param>
        /// <param name="primary">The primary operation.</param>
        /// <returns>The Instruction instance.</returns>
        private static InstructionInstance UnpatchedHost(int hostID, QuantityChangeOperation primary)
        {
            return new InstructionInstance(new InstanceID(hostID), InstructionWith(primary), null);
        }

        /// <summary>
        /// A patched host: the Instruction instance with the given primary operation carrying one
        /// socketed Patch instance declaring one effect.
        /// </summary>
        /// <param name="hostID">The host's instance identity value.</param>
        /// <param name="patchID">The Patch instance identity value.</param>
        /// <param name="patchDefinitionID">The Patch definition's surrogate-key identity.</param>
        /// <param name="effect">The Patch's declared effect.</param>
        /// <param name="primary">The host's primary operation.</param>
        private static InstructionInstance PatchedHost(
            int hostID,
            int patchID,
            string patchDefinitionID,
            EffectDefinition effect,
            QuantityChangeOperation primary)
        {
            PatchDefinition definition = new PatchDefinition(
                new PatchID(patchDefinitionID),
                "Test rules.",
                "TEST PATCH",
                ContentCategory.Patch,
                Rarity.Common,
                new List<string>(),
                new PatchHostEligibility("ORDINARY_INSTRUCTION_HOSTS"),
                new List<EffectDefinition> { effect });

            return new InstructionInstance(
                new InstanceID(hostID),
                InstructionWith(primary),
                new PatchInstance(new InstanceID(patchID), definition));
        }

        /// <summary>
        /// Builds a fixture Instruction definition around a primary operation.
        /// </summary>
        /// <param name="primary">The primary operation.</param>
        /// <returns>The frozen definition.</returns>
        private static InstructionDefinition InstructionWith(QuantityChangeOperation primary)
        {
            return new InstructionDefinition(
                new InstructionID("WB-INS-FIXTURE"),
                "rules",
                "instruction",
                ContentCategory.Instruction,
                Rarity.Common,
                new List<string>(),
                1,
                primary,
                null,
                new List<string>());
        }

        /// <summary>
        /// The CONSTANT-PATCH-shaped effect: the host's fixed-addition pending operation gains 1 at
        /// the modification band.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition ConstantPatchEffect()
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Operation,
                "PRIMARY_OPERATION_PENDING",
                new List<TriggerQualifier> { new TriggerQualifier("OPERATION_CLASS", "FIXED_ADDITION") },
                timing);

            return new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new OperationModificationOperation(1),
                new TargetingRule("OWN_HOST", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
        }

        /// <summary>
        /// The FEEDBACK-PATCH-shaped effect: the host's positive Score change adds 1 to Value at the
        /// immediate-reaction band.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition FeedbackPatchEffect()
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                    new TriggerQualifier("REGISTER", "SCORE"),
                    new TriggerQualifier("OPERATION_CLASS", "HOST_INSTRUCTION")
                },
                timing);

            return new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new TargetingRule("NO_TARGET", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
        }

        /// <summary>
        /// An OUTPUT-CACHE-shaped Dependency instance: every positive Score change adds 1 to Value
        /// at the immediate-reaction band.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance OutputCacheInstance(int instance)
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                    new TriggerQualifier("REGISTER", "SCORE")
                },
                timing);

            EffectDefinition effect = new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));

            DependencyDefinition definition = new DependencyDefinition(
                new DependencyID("WB-DEP-005"),
                "Test rules.",
                "TEST DEPENDENCY",
                ContentCategory.Dependency,
                Rarity.Starter,
                new List<string>(),
                0,
                new List<EffectDefinition> { effect });

            return new DependencyInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// The ECHO-PATCH-shaped effect: the even-line host closure creates one own-host descendant,
        /// once per source execution.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition EchoPatchEffect()
        {
            return PostUnitPatchEffect("POSITIONAL", "EVEN_NUMBERED_LINE");
        }

        /// <summary>
        /// The TERMINAL-PATCH-shaped effect: the final-occupied-player-line host closure creates one
        /// own-host descendant, once per source execution.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition TerminalPatchEffect()
        {
            return PostUnitPatchEffect("POSITIONAL", "FINAL_OCCUPIED_PLAYER_LINE");
        }

        /// <summary>
        /// The TRUE-PATCH-shaped effect: the succeeding-Condition host closure creates one own-host
        /// descendant, once per source execution.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition TruePatchEffect()
        {
            return PostUnitPatchEffect("STRUCTURE_CONTEXT", "INSIDE_SUCCEEDING_CONDITION");
        }

        /// <summary>
        /// The PIPELINE-PATCH-shaped effect: the adjacency-qualified host closure creates one
        /// own-host descendant, once per source execution.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition PipelinePatchEffect()
        {
            return PostUnitPatchEffect("STRUCTURE_CONTEXT", "ADJACENT_AFTER_SUCCESSFUL_SCORE");
        }

        /// <summary>
        /// Builds a post-unit Patch creator carrying one qualifier and own-host targeting — the
        /// shape the four added-execution Patches share.
        /// </summary>
        /// <param name="qualifierKind">The qualifier kind token.</param>
        /// <param name="qualifierValue">The qualifier value token.</param>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition PostUnitPatchEffect(string qualifierKind, string qualifierValue)
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Lifecycle,
                "RUNTIME_UNIT_COMPLETED",
                new List<TriggerQualifier> { new TriggerQualifier(qualifierKind, qualifierValue) },
                timing);

            return new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new AddedExecutionRequestOperation(new TargetingRule("OWN_HOST", string.Empty), false),
                new TargetingRule("OWN_HOST", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("ONCE", "SOURCE_EXECUTION"));
        }

        /// <summary>
        /// Resolves a unit's child events in append order.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="unit">The unit whose events to resolve.</param>
        /// <returns>The unit's events.</returns>
        private static List<EventEvidence> UnitEvents(ExecutionRecord record, RuntimeUnitRecord unit)
        {
            List<EventEvidence> events = new List<EventEvidence>(unit.ChildEvents.Count);
            for (int i = 0; i < unit.ChildEvents.Count; i++)
            {
                events.Add(record.Events[unit.ChildEvents[i].Value - 1].Evidence);
            }

            return events;
        }

        /// <summary>
        /// Returns the index of the first event carrying the subtype, or -1 when none does.
        /// </summary>
        /// <param name="events">The events to scan.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The index, or -1.</returns>
        private static int IndexOfSubtype(List<EventEvidence> events, string subtype)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Subtype == subtype)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Returns every index carrying the subtype, in order.
        /// </summary>
        /// <param name="events">The events to scan.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The matching indexes.</returns>
        private static List<int> IndexesOfSubtype(List<EventEvidence> events, string subtype)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Subtype == subtype)
                    indexes.Add(i);
            }

            return indexes;
        }

        /// <summary>
        /// Returns the index of the quantity event a named effect produced, or -1 when none exists.
        /// </summary>
        /// <param name="events">The events to scan.</param>
        /// <param name="origin">The effect's origin instance.</param>
        /// <returns>The index, or -1.</returns>
        private static int IndexOfReactionQuantity(List<EventEvidence> events, InstanceID origin)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Subtype == ExecutionEventSubtypes.QuantityChanged
                    && events[i].EffectOriginInstance == origin)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds the first record-wide event carrying the subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The event's evidence.</returns>
        private static EventEvidence FindGlobal(ExecutionRecord record, string subtype)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == subtype)
                    return record.Events[i].Evidence;
            }

            Assert.Fail($"No event carries subtype {subtype}.");
            return null;
        }

        /// <summary>
        /// Counts the record-wide events carrying the subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The count.</returns>
        private static int CountGlobal(ExecutionRecord record, string subtype)
        {
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == subtype)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Counts the record-wide request events a named creator produced.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="origin">The creator's origin instance.</param>
        /// <returns>The count.</returns>
        private static int CountRequestsOf(ExecutionRecord record, InstanceID origin)
        {
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype == ExecutionEventSubtypes.AddedExecutionRequested
                    && evidence.EffectOriginInstance == origin)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Counts the record-wide near-misses carrying the failed-requirement reason.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="reason">The failed-requirement reason string.</param>
        /// <returns>The count.</returns>
        private static int CountNearMissReason(ExecutionRecord record, string reason)
        {
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype == ExecutionEventSubtypes.EffectFailedToQualify
                    && evidence.DispositionReason == reason)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Returns every near-miss a named effect produced, in record order.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="origin">The effect's origin instance.</param>
        /// <returns>The matching near-miss evidence.</returns>
        private static List<EventEvidence> NearMissesOf(ExecutionRecord record, InstanceID origin)
        {
            List<EventEvidence> matches = new List<EventEvidence>();
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype == ExecutionEventSubtypes.EffectFailedToQualify
                    && evidence.EffectOriginInstance == origin)
                {
                    matches.Add(evidence);
                }
            }

            return matches;
        }

        /// <summary>
        /// Returns every chain event of the subtype a named effect produced, in record order.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <param name="origin">The effect's origin instance.</param>
        /// <returns>The matching evidence.</returns>
        private static List<EventEvidence> ChainEventsOf(
            ExecutionRecord record,
            string subtype,
            InstanceID origin)
        {
            List<EventEvidence> matches = new List<EventEvidence>();
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype == subtype && evidence.EffectOriginInstance == origin)
                    matches.Add(evidence);
            }

            return matches;
        }

        /// <summary>
        /// Counts the record-wide chain events of the subtype a named effect produced.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <param name="origin">The effect's origin instance.</param>
        /// <returns>The count.</returns>
        private static int CountChainEvents(
            ExecutionRecord record,
            string subtype,
            InstanceID origin)
        {
            return ChainEventsOf(record, subtype, origin).Count;
        }
    }
}
