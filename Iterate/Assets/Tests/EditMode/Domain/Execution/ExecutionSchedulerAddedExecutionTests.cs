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
    /// Tests the immediate-descendant pipeline end to end: OVERCLOCK's in-unit qualification,
    /// commitment, and request chain followed by the descendant unit resolved after the causing unit
    /// closes; LOOP UNROLLER's unit-less post-unit chain with retained iteration context; sibling
    /// batches resolving in commitment order; and the origin lock's termination shape for an
    /// every-qualifying creator.
    /// </summary>
    public sealed class ExecutionSchedulerAddedExecutionTests
    {
        [Test]
        public void Overclock_PlayerValueGain_ResolvesOneDescendantAfterTheCausingUnit()
        {
            ExecutionRecord record = Execute(OverclockRequest());

            Assert.AreEqual(3, record.Units.Count);
            RuntimeUnitRecord causing = record.Units[1];

            List<EventEvidence> causingEvents = UnitEvents(record, causing);
            int requested = IndexOfSubtype(causingEvents, ExecutionEventSubtypes.AddedExecutionRequested);
            int qualified = IndexOfSubtype(causingEvents, ExecutionEventSubtypes.EffectQualified);
            int committed = IndexOfSubtype(causingEvents, ExecutionEventSubtypes.EffectCommitted);
            int completed = IndexOfSubtype(causingEvents, ExecutionEventSubtypes.SourceExecutionCompleted);

            Assert.Greater(requested, committed, "the request follows commitment");
            Assert.Greater(committed, qualified, "commitment follows qualification");
            Assert.Less(requested, completed, "the request is created inside the causing unit");
            Assert.AreEqual(EventFamilies.AddedExecution, causingEvents[requested].Family);
            Assert.AreEqual(causing.Identity, causingEvents[requested].ContainingUnit);
        }

        [Test]
        public void Overclock_Descendant_StartsAndCompletesOutsideTheCausingUnit()
        {
            ExecutionRecord record = Execute(OverclockRequest());
            RuntimeUnitRecord causing = record.Units[1];
            RuntimeUnitRecord descendant = record.Units[2];

            EventEvidence started = FindGlobal(record, ExecutionEventSubtypes.AddedExecutionStarted);
            EventEvidence finished = FindGlobal(record, ExecutionEventSubtypes.AddedExecutionCompleted);

            Assert.IsNull(started.ContainingUnit);
            Assert.IsNull(finished.ContainingUnit);
            Assert.AreEqual(1, started.CausalDepth);
            Assert.AreEqual(1, finished.CausalDepth);
            Assert.Less(GlobalIndex(record, started), FirstEventIndex(record, descendant));
            Assert.Greater(GlobalIndex(record, finished), LastEventIndex(record, descendant));
            Assert.Greater(GlobalIndex(record, started), LastEventIndex(record, causing));
        }

        [Test]
        public void Overclock_DescendantOpening_CarriesEveryAddedExecutionField()
        {
            ExecutionRecord record = Execute(OverclockRequest());
            RuntimeUnitRecord causing = record.Units[1];
            RuntimeUnitOpening opening = record.Units[2].Opening;

            Assert.AreEqual(ActivationKind.AddedExecution, opening.Activation);
            Assert.AreEqual(
                "WB-DIR-001:0#900@e" + TriggeringQuantityOrdinal(record, causing),
                opening.OriginatingRequestIdentity);
            Assert.AreEqual(1, opening.AddedExecutionDepth);
            Assert.AreEqual(causing.Identity, opening.ParentUnit);
            Assert.AreEqual(EffectOriginLineage.Empty.Append(new InstanceID(900)), opening.Lineage);
        }

        [Test]
        public void Overclock_DescendantEvents_CarryTheBranchLineage()
        {
            ExecutionRecord record = Execute(OverclockRequest());
            EffectOriginLineage expected = EffectOriginLineage.Empty.Append(new InstanceID(900));

            List<EventEvidence> descendantEvents = UnitEvents(record, record.Units[2]);
            for (int i = 0; i < descendantEvents.Count; i++)
            {
                Assert.AreEqual(expected, descendantEvents[i].Lineage, descendantEvents[i].Subtype);
            }
        }

        [Test]
        public void Overclock_Registers_LandTheDuplicatedGain()
        {
            ExecutionRecord record = Execute(OverclockRequest());

            Assert.AreEqual(new ValueAmount(5), record.FinalState.FinalValue);
        }

        [Test]
        public void Overclock_ConsumedReOfferInsideTheDescendant_IsSilent()
        {
            ExecutionRecord record = Execute(OverclockRequest());

            List<EventEvidence> descendantEvents = UnitEvents(record, record.Units[2]);

            Assert.AreEqual(-1, IndexOfSubtype(descendantEvents, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(-1, IndexOfSubtype(descendantEvents, ExecutionEventSubtypes.EffectFailedToQualify));
            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void Overclock_CoreOwnedValueGain_NearMissesAndCreatesNoRequest()
        {
            ExecutionRecord record = Execute(OverclockRequest());

            List<EventEvidence> coreEvents = UnitEvents(record, record.Units[0]);
            int nearMiss = IndexOfSubtype(coreEvents, ExecutionEventSubtypes.EffectFailedToQualify);

            Assert.AreNotEqual(-1, nearMiss);
            Assert.AreEqual("OPERATION_CLASS:PLAYER_INSTRUCTION", coreEvents[nearMiss].DispositionReason);
            Assert.AreEqual(-1, IndexOfSubtype(coreEvents, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void Overclock_ReactionCausedValueGain_IsStructurallySilent()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.SignalAddInstance(10, 1))
            });
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                SchedulerFixtures.ParallelChannelInstance(400)
            };
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>
            {
                SchedulerFixtures.OverclockPragma(900)
            };

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, installed, pragmas));

            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));

            TraceEventID reactionQuantity = ReactionQuantityEventIdentity(record, new InstanceID(400));
            List<EventEvidence> events = UnitEvents(record, record.Units[0]);
            for (int i = 0; i < events.Count; i++)
            {
                EventEvidence evidence = events[i];
                if (evidence.Subtype != ExecutionEventSubtypes.EffectFailedToQualify)
                    continue;

                if (evidence.CausingEvent == reactionQuantity)
                    Assert.AreNotEqual(new InstanceID(900), evidence.EffectOriginInstance, "creators never near-miss a reaction-caused change");
            }
        }

        [Test]
        public void Overclock_PrimarySignalGain_NearMissesRegisterValue()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.SignalAddInstance(10, 1))
            });
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>
            {
                SchedulerFixtures.OverclockPragma(900)
            };

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), pragmas));

            List<EventEvidence> events = UnitEvents(record, record.Units[0]);
            int nearMiss = IndexOfSubtype(events, ExecutionEventSubtypes.EffectFailedToQualify);

            Assert.AreNotEqual(-1, nearMiss, "a Signal gain is an eligible candidate failing the register qualifier");
            Assert.AreEqual(new InstanceID(900), events[nearMiss].EffectOriginInstance);
            Assert.AreEqual("REGISTER:VALUE", events[nearMiss].DispositionReason);
            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void Overclock_SecondEligibleUnit_IsUnduplicated()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(10, 2)),
                SourceSlot.ForInstruction(new SourcePosition(2), SchedulerFixtures.ValueAddInstance(11, 3))
            });
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>
            {
                SchedulerFixtures.OverclockPragma(900)
            };

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), pragmas));

            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(3, record.Units.Count);
            Assert.AreEqual(new ValueAmount(7), record.FinalState.FinalValue);
        }

        [Test]
        public void LoopUnroller_FirstRepeatChild_ResolvesADescendantBeforeIterationCompletion()
        {
            ExecutionRecord record = Execute(LoopUnrollerRequest());

            Assert.AreEqual(3, record.Units.Count);
            EventEvidence requested = FindGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested);
            Assert.IsNull(requested.ContainingUnit);

            int firstIterationCompleted = FirstGlobalIndex(record, ExecutionEventSubtypes.RepeatIterationCompleted);
            Assert.Less(LastEventIndex(record, record.Units[1]), firstIterationCompleted);
            Assert.Less(GlobalIndex(record, FindGlobal(record, ExecutionEventSubtypes.AddedExecutionCompleted)), firstIterationCompleted);
        }

        [Test]
        public void LoopUnroller_Descendant_RetainsTheIterationContext()
        {
            ExecutionRecord record = Execute(LoopUnrollerRequest());

            StructureContext causingContext = record.Units[0].Opening.StructureContext;
            StructureContext descendantContext = record.Units[1].Opening.StructureContext;

            Assert.IsNotNull(descendantContext);
            Assert.AreEqual(causingContext.RepeatIterationIdentity, descendantContext.RepeatIterationIdentity);
            Assert.AreEqual(ActivationKind.AddedExecution, record.Units[1].Opening.Activation);
        }

        [Test]
        public void LoopUnroller_SecondIteration_IsUnduplicated()
        {
            ExecutionRecord record = Execute(LoopUnrollerRequest());

            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(new ValueAmount(3), record.FinalState.FinalValue);
        }

        [Test]
        public void LoopUnroller_TopLevelUnit_NearMissesStructureContext()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(10, 1))
            });
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                SchedulerFixtures.LoopUnrollerInstance(800)
            };

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, installed));

            EventEvidence nearMiss = FindGlobal(record, ExecutionEventSubtypes.EffectFailedToQualify);
            Assert.AreEqual("STRUCTURE_CONTEXT:INSIDE_REPEAT", nearMiss.DispositionReason);
            Assert.IsNull(nearMiss.ContainingUnit);
            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void SiblingBatch_OverclockThenLoopUnroller_ResolveInCommitmentOrder()
        {
            ExecutionRecord record = Execute(SiblingBatchRequest());

            Assert.AreEqual(3, record.Units.Count);
            Assert.AreEqual(2, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));

            RuntimeUnitRecord first = record.Units[1];
            RuntimeUnitRecord second = record.Units[2];
            Assert.Less(LastEventIndex(record, first), FirstEventIndex(record, second), "each branch closes before the next begins");
            Assert.AreEqual(
                EffectOriginLineage.Empty.Append(new InstanceID(900)),
                first.Opening.Lineage);
            Assert.AreEqual(
                EffectOriginLineage.Empty.Append(new InstanceID(800)),
                second.Opening.Lineage);
        }

        [Test]
        public void SiblingBatch_Registers_LandBothDuplications()
        {
            ExecutionRecord record = Execute(SiblingBatchRequest());

            Assert.AreEqual(new ValueAmount(6), record.FinalState.FinalValue);
        }

        [Test]
        public void OriginLock_EveryQualifyingCreator_GivesEachUnitExactlyOneDescendant()
        {
            ExecutionRecord record = Execute(EveryQualifyingRequest());

            Assert.AreEqual(4, record.Units.Count);
            Assert.AreEqual(2, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
        }

        [Test]
        public void OriginLock_DescendantOwnGain_YieldsTheOriginLockNearMiss()
        {
            ExecutionRecord record = Execute(EveryQualifyingRequest());

            List<EventEvidence> descendantEvents = UnitEvents(record, record.Units[1]);
            int nearMiss = IndexOfSubtype(descendantEvents, ExecutionEventSubtypes.EffectFailedToQualify);

            Assert.AreNotEqual(-1, nearMiss);
            Assert.AreEqual("ORIGIN_LOCK:" + new InstanceID(900).ToString(), descendantEvents[nearMiss].DispositionReason);
            Assert.AreEqual(-1, IndexOfSubtype(descendantEvents, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void OriginLock_SafetyCounts_ReportDepthOneAndTwoDescendants()
        {
            ExecutionRecord record = Execute(EveryQualifyingRequest());

            Assert.AreEqual(1, record.SafetyCounts.LineageDepthHighWater);
            Assert.AreEqual(2, record.SafetyCounts.AddedDescendants);
            Assert.AreEqual(4, record.SafetyCounts.SourceExecutionUnits);
        }

        /// <summary>
        /// A Core <c>Value = 1</c> line followed by a player <c>Value += 2</c> Instruction, with the
        /// OVERCLOCK pragma active: the Core gain near-misses, the player gain duplicates once.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest OverclockRequest()
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
                SchedulerFixtures.OverclockPragma(900)
            };

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), pragmas);
        }

        /// <summary>
        /// A <c>Repeat 2</c> over one contained <c>Value += 1</c> child with LOOP UNROLLER installed:
        /// iteration 1's child duplicates, iteration 2's does not.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest LoopUnrollerRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance repeat = SchedulerFixtures.RepeatStructure(20, 2, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), repeat),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), repeat, SchedulerFixtures.ValueAddInstance(10, 1))
            });
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                SchedulerFixtures.LoopUnrollerInstance(800)
            };

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, installed);
        }

        /// <summary>
        /// A <c>Repeat 1</c> over one contained <c>Value += 2</c> child with both the OVERCLOCK
        /// pragma and LOOP UNROLLER installed: two requests from one child, resolved in commitment
        /// order.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest SiblingBatchRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance repeat = SchedulerFixtures.RepeatStructure(20, 1, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), repeat),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), repeat, SchedulerFixtures.ValueAddInstance(10, 2))
            });
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                SchedulerFixtures.LoopUnrollerInstance(800)
            };
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>
            {
                SchedulerFixtures.OverclockPragma(900)
            };

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, installed, pragmas);
        }

        /// <summary>
        /// Two top-level <c>Value += 1</c> Instructions with an every-qualifying creator pragma: each
        /// canonical unit gains exactly one descendant, each descendant lock-blocked.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest EveryQualifyingRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(10, 1)),
                SourceSlot.ForInstruction(new SourcePosition(2), SchedulerFixtures.ValueAddInstance(11, 1))
            });
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>
            {
                SchedulerFixtures.EveryQualifyingCreatorPragma(900)
            };

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), pragmas);
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

        /// <summary>
        /// Returns the record-wide index of an evidence value by identity scan.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="evidence">The evidence to locate.</param>
        /// <returns>The index.</returns>
        private static int GlobalIndex(ExecutionRecord record, EventEvidence evidence)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (ReferenceEquals(record.Events[i].Evidence, evidence))
                    return i;
            }

            Assert.Fail("The evidence is not in the record.");
            return -1;
        }

        /// <summary>
        /// Returns the record-wide index of the unit's first child event.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="unit">The unit.</param>
        /// <returns>The index.</returns>
        private static int FirstEventIndex(ExecutionRecord record, RuntimeUnitRecord unit)
        {
            return unit.ChildEvents[0].Value - 1;
        }

        /// <summary>
        /// Returns the record-wide index of the unit's last child event.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="unit">The unit.</param>
        /// <returns>The index.</returns>
        private static int LastEventIndex(ExecutionRecord record, RuntimeUnitRecord unit)
        {
            return unit.ChildEvents[unit.ChildEvents.Count - 1].Value - 1;
        }

        /// <summary>
        /// Returns the ordinal of the unit's primary quantity event — the request's triggering event,
        /// from which the deterministic request identity is composed.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="unit">The unit whose primary quantity event to find.</param>
        /// <returns>The event ordinal.</returns>
        private static int TriggeringQuantityOrdinal(ExecutionRecord record, RuntimeUnitRecord unit)
        {
            for (int i = 0; i < unit.ChildEvents.Count; i++)
            {
                EventRecord entry = record.Events[unit.ChildEvents[i].Value - 1];
                if (entry.Evidence.Subtype == ExecutionEventSubtypes.QuantityChanged
                    && entry.Evidence.EffectOriginInstance == null)
                {
                    return entry.Identity.Value;
                }
            }

            Assert.Fail("The unit has no primary quantity event.");
            return 0;
        }

        /// <summary>
        /// Returns the identity of the quantity event a named reacting effect produced.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="reactionOrigin">The reacting effect's origin instance.</param>
        /// <returns>The reaction's quantity-event identity.</returns>
        private static TraceEventID ReactionQuantityEventIdentity(ExecutionRecord record, InstanceID reactionOrigin)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventRecord entry = record.Events[i];
                if (entry.Evidence.Subtype == ExecutionEventSubtypes.QuantityChanged
                    && entry.Evidence.EffectOriginInstance == reactionOrigin)
                {
                    return entry.Identity;
                }
            }

            Assert.Fail("No reaction-caused quantity event exists.");
            return new TraceEventID(1);
        }
    }
}
