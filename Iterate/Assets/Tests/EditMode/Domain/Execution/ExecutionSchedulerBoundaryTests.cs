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
    /// Tests the end-of-player-controlled-traversal boundary: ALIGN's request/resolve/quantity chain
    /// landing after the last player unit's descendants and before any trailing Core-owned line, the
    /// even-Value near-miss consuming nothing, the vacuous boundary when no player position exists,
    /// and reactions to a boundary effect's own quantity change resolving source-lessly.
    /// </summary>
    public sealed class ExecutionSchedulerBoundaryTests
    {
        [Test]
        public void OddPlayerValue_ResolvesTheFullBoundaryChain()
        {
            ExecutionRecord record = Execute(AlignRequest(3));

            EventEvidence requested = FindGlobal(record, ExecutionEventSubtypes.BoundaryEffectRequested);
            EventEvidence resolved = FindGlobal(record, ExecutionEventSubtypes.BoundaryEffectResolved);

            Assert.AreEqual(EventFamilies.Reaction, requested.Family);
            Assert.AreEqual(0, requested.CausalDepth);
            Assert.IsNull(requested.CausingEvent);
            Assert.IsNull(requested.ContainingUnit);
            Assert.AreEqual(new InstanceID(950), requested.EffectOriginInstance);

            Assert.AreEqual(1, resolved.CausalDepth);
            Assert.IsNull(resolved.ContainingUnit);
        }

        [Test]
        public void OddPlayerValue_BoundaryQuantity_IsSourceLessAtDepthTwo()
        {
            ExecutionRecord record = Execute(AlignRequest(3));

            EventEvidence quantity = BoundaryQuantity(record, new InstanceID(950));

            Assert.AreEqual(2, quantity.CausalDepth);
            Assert.IsNull(quantity.ContainingUnit);
            Assert.IsNull(quantity.Ownership);
            Assert.IsNull(quantity.HostInstance);
            Assert.IsNull(quantity.Position);
            Assert.AreEqual(new InstanceID(950), quantity.EffectOriginInstance);
        }

        [Test]
        public void OddPlayerValue_LandsBeforeTheTrailingCoreLine()
        {
            ExecutionRecord record = Execute(AlignRequest(3));

            int boundary = FirstGlobalIndex(record, ExecutionEventSubtypes.BoundaryEffectRequested);
            RuntimeUnitRecord coreUnit = record.Units[record.Units.Count - 1];

            Assert.AreEqual(OwnershipClassification.CoreOwned, coreUnit.Opening.Ownership);
            Assert.Less(boundary, coreUnit.ChildEvents[0].Value - 1, "the boundary resolves before traversal resumes into Core");
        }

        [Test]
        public void OddPlayerValue_RegistersLandTheAlignedValue()
        {
            ExecutionRecord record = Execute(AlignRequest(3));

            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(4), record.FinalState.FinalOutput);
        }

        [Test]
        public void EvenPlayerValue_TakesTheParityNearMissOnly()
        {
            ExecutionRecord record = Execute(AlignRequest(2));

            EventEvidence nearMiss = FindGlobal(record, ExecutionEventSubtypes.EffectFailedToQualify);

            Assert.AreEqual("PARITY:ODD", nearMiss.DispositionReason);
            Assert.AreEqual(new InstanceID(950), nearMiss.EffectOriginInstance);
            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.BoundaryEffectRequested));
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(2), record.FinalState.FinalOutput);
        }

        [Test]
        public void NoPlayerPositions_BoundaryResolvesBeforeTheFirstCoreUnit()
        {
            InstanceIDSource ids = new InstanceIDSource();
            CoreLine core = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(5)));
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), core)
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), AlignOnly()));

            int nearMiss = FirstGlobalIndex(record, ExecutionEventSubtypes.EffectFailedToQualify);

            Assert.Less(nearMiss, record.Units[0].ChildEvents[0].Value - 1, "with no player position the boundary is reached at traversal start");
            Assert.AreEqual(new ValueAmount(5), record.FinalState.FinalValue);
        }

        [Test]
        public void BoundaryEffect_QualifyingReaction_ResolvesSourceLessly()
        {
            ExecutionRecord record = Execute(SignalBoundaryRequest());

            EventEvidence boundaryQuantity = BoundaryQuantity(record, new InstanceID(951));
            EventEvidence reactionQuantity = BoundaryQuantity(record, new InstanceID(400));

            Assert.AreEqual(2, boundaryQuantity.CausalDepth);
            Assert.AreEqual(4, reactionQuantity.CausalDepth);
            Assert.IsNull(reactionQuantity.ContainingUnit);
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
            Assert.AreEqual(new SignalValue(1), record.FinalState.FinalSignal);
        }

        [Test]
        public void BoundaryEffect_ReactionChain_IsUnitLessAtDepthThree()
        {
            ExecutionRecord record = Execute(SignalBoundaryRequest());

            EventEvidence reaction = FindGlobal(record, ExecutionEventSubtypes.ImmediateReactionResolved);

            Assert.AreEqual(3, reaction.CausalDepth);
            Assert.IsNull(reaction.ContainingUnit);
            Assert.AreEqual(new InstanceID(400), reaction.EffectOriginInstance);
        }

        [Test]
        public void Creators_AreStructurallySilentAtTheBoundaryQuantity()
        {
            ExecutionRecord record = Execute(AlignWithOverclockRequest());

            EventEvidence boundaryQuantity = BoundaryQuantity(record, new InstanceID(950));
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype != ExecutionEventSubtypes.EffectFailedToQualify)
                    continue;

                if (evidence.CausingEvent == EventIdentityOf(record, boundaryQuantity))
                    Assert.AreNotEqual(new InstanceID(900), evidence.EffectOriginInstance, "creators never see a source-less change");
            }
        }

        [Test]
        public void AlignWithOverclock_FiresEachExactlyOnce()
        {
            ExecutionRecord record = Execute(AlignWithOverclockRequest());

            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.BoundaryEffectRequested));
            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(new ValueAmount(6), record.FinalState.FinalValue);
        }

        [Test]
        public void AlignWithOverclock_DoubleRun_IsStructurallyEqual()
        {
            ExecutionRecord first = Execute(AlignWithOverclockRequest());
            ExecutionRecord second = Execute(AlignWithOverclockRequest());

            Assert.AreEqual(first.Events.Count, second.Events.Count);
            Assert.AreEqual(first.FinalState, second.FinalState);
            Assert.AreEqual(first.SafetyCounts, second.SafetyCounts);
            for (int i = 0; i < first.Events.Count; i++)
            {
                Assert.AreEqual(first.Events[i].Evidence, second.Events[i].Evidence, "event " + i);
            }
        }

        /// <summary>
        /// One player <c>Value += constant</c> Instruction followed by a Core <c>Score = Value</c>
        /// line, under the ALIGN pragma. The Core line reads Value after the boundary, so an aligned
        /// Value is observable in Score.
        /// </summary>
        /// <param name="constant">The player Instruction's fixed operand.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest AlignRequest(int constant)
        {
            InstanceIDSource ids = new InstanceIDSource();
            CoreLine core = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(10, constant)),
                SourceSlot.ForCore(new SourcePosition(2), core)
            });

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), AlignOnly());
        }

        /// <summary>
        /// One player <c>Value += 1</c> Instruction under a Signal-adding boundary Directive with
        /// PARALLEL CHANNEL installed: the boundary's own Signal gain is offered to the reaction
        /// matcher outside any unit.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest SignalBoundaryRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(10, 1))
            });
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                SchedulerFixtures.ParallelChannelInstance(400)
            };
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>
            {
                SchedulerFixtures.SignalBoundaryPragma(951)
            };

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), ids, installed, pragmas);
        }

        /// <summary>
        /// A Core <c>Value = 1</c> line then a player <c>Value += 2</c> Instruction, under both the
        /// ALIGN and OVERCLOCK pragmas: Core 0→1, player 1→3, the OVERCLOCK descendant 3→5, then the
        /// boundary aligns the odd 5 to 6.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest AlignWithOverclockRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            CoreLine core = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), core),
                SourceSlot.ForInstruction(new SourcePosition(2), SchedulerFixtures.ValueAddInstance(10, 2))
            });
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>
            {
                SchedulerFixtures.OverclockPragma(900),
                SchedulerFixtures.AlignPragma(950)
            };

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), pragmas);
        }

        /// <summary>
        /// The ALIGN pragma alone.
        /// </summary>
        /// <returns>The pragma list.</returns>
        private static List<DirectiveInstance> AlignOnly()
        {
            return new List<DirectiveInstance> { SchedulerFixtures.AlignPragma(950) };
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
        /// Finds the unit-less quantity event a named effect produced outside any unit.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="origin">The producing effect's origin instance.</param>
        /// <returns>The event's evidence.</returns>
        private static EventEvidence BoundaryQuantity(ExecutionRecord record, InstanceID origin)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype == ExecutionEventSubtypes.QuantityChanged
                    && evidence.ContainingUnit == null
                    && evidence.EffectOriginInstance == origin)
                {
                    return evidence;
                }
            }

            Assert.Fail("No unit-less quantity event carries that origin.");
            return null;
        }

        /// <summary>
        /// Returns the identity of an evidence value already located in the record.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="evidence">The evidence to locate.</param>
        /// <returns>The event identity.</returns>
        private static TraceEventID EventIdentityOf(ExecutionRecord record, EventEvidence evidence)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (ReferenceEquals(record.Events[i].Evidence, evidence))
                    return record.Events[i].Identity;
            }

            Assert.Fail("The evidence is not in the record.");
            return new TraceEventID(1);
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
        /// Returns the record-wide index of the first event carrying the subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The index.</returns>
        private static int FirstGlobalIndex(ExecutionRecord record, string subtype)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == subtype)
                    return i;
            }

            Assert.Fail($"No event carries subtype {subtype}.");
            return -1;
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
    }
}
