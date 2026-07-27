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

        [Test]
        public void BurstOutput_LocksScoreHost_ResolvesTheDescendantBranch()
        {
            // One player Score += 3 host under BURST OUTPUT: locked at window close, its branch
            // re-executes the host — Score 0 → 3 → 6.
            ExecutionRecord record = Execute(BurstRequest());

            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionTargetLocked));
            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(2, record.Units.Count);
            Assert.AreEqual(new ScoreValue(6), record.FinalState.FinalScore);
        }

        [Test]
        public void BurstOutput_TargetLocked_IsUncausedAtDepthZeroNamingTheHost()
        {
            ExecutionRecord record = Execute(BurstRequest());

            EventEvidence locked = FindGlobal(record, ExecutionEventSubtypes.AddedExecutionTargetLocked);

            Assert.AreEqual(EventFamilies.AddedExecution, locked.Family);
            Assert.AreEqual(0, locked.CausalDepth);
            Assert.IsNull(locked.CausingEvent);
            Assert.IsNull(locked.ContainingUnit);
            Assert.IsNull(locked.AddedExecutionRequestIdentity, "the token precedes the request and carries no request identity");
            Assert.AreEqual(0, locked.AddedExecutionDepth);
            Assert.AreEqual(new InstanceID(900), locked.EffectOriginInstance);
            Assert.AreEqual(new InstanceID(10), locked.HostInstance, "position/ownership from the locked slot");
            Assert.AreEqual(new InstanceID(10).ToString(), locked.TargetIdentity, "target identity is the locked host instance");
        }

        [Test]
        public void BurstOutput_RequestChain_FollowsTheTargetLockedEventAtDepthOne()
        {
            ExecutionRecord record = Execute(BurstRequest());

            EventEvidence locked = FindGlobal(record, ExecutionEventSubtypes.AddedExecutionTargetLocked);
            EventEvidence requested = FindGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested);

            Assert.AreEqual(1, requested.CausalDepth);
            Assert.AreEqual(EventIdentityOf(record, locked), requested.CausingEvent, "the request is caused by the TARGET_LOCKED event");
            Assert.AreEqual(new InstanceID(10), requested.HostInstance);
        }

        [Test]
        public void BurstOutput_Descendant_DescendsFromTheLockedUnitAtDepthOne()
        {
            ExecutionRecord record = Execute(BurstRequest());

            RuntimeUnitRecord descendant = record.Units[1];

            Assert.AreEqual(ActivationKind.AddedExecution, descendant.Opening.Activation);
            Assert.AreEqual(1, descendant.Opening.AddedExecutionDepth);
            Assert.AreEqual(record.Units[0].Identity, descendant.Opening.ParentUnit,
                "the boundary descendant carries the locked unit as its record parent (RuntimeUnitOpening requires one); its safety root activation is its own unit");
        }

        [Test]
        public void BurstOutput_WindowCloses_NoLockUpdateCommitInsideTheBurstBranch()
        {
            // The descendant's own Score gain would re-qualify the every-qualifying lock update were
            // the window open; the closed window leaves exactly the one traversal-time commit.
            ExecutionRecord record = Execute(BurstRequest());

            Assert.AreEqual(1, CountChainEvents(record, ExecutionEventSubtypes.EffectCommitted, new InstanceID(900)),
                "only the traversal-time lock update commits; the Burst branch's Score gain does not re-lock");
        }

        [Test]
        public void BurstOutput_LocksTheMostRecentQualifyingHost()
        {
            // Two Score hosts; the second (id 10, position 2) is the most recent qualifying event, so
            // the branch re-executes it — not the earlier id 20.
            ExecutionRecord record = Execute(BurstOverwriteRequest());

            EventEvidence locked = FindGlobal(record, ExecutionEventSubtypes.AddedExecutionTargetLocked);
            Assert.AreEqual(new InstanceID(10), locked.HostInstance);
            Assert.AreEqual(new SourcePosition(2), record.Units[2].Opening.Position, "the descendant re-executes the most recent host");
            Assert.AreEqual(new ScoreValue(12), record.FinalState.FinalScore);
        }

        [Test]
        public void BurstOutput_NoQualifyingScore_YieldsNoLockNoRequest()
        {
            // A Value-only player Instruction never qualifies the Score lock: no token, no request,
            // the once-per-execution allowance intact (no commitment event).
            ExecutionRecord record = Execute(BurstNoScoreRequest());

            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionTargetLocked));
            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(1, record.Units.Count);
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
        }

        [Test]
        public void BurstOutput_LockedHostFeedbackFlipsParity_AlignQualifiesOnTheFreshOffer()
        {
            // Pre-Burst Value is even (2); the locked host's FEEDBACK PATCH flips it to odd (3) inside
            // the Burst branch, and ALIGN's fresh offer reads the flipped 3 → qualifies → 4.
            ExecutionRecord record = Execute(BurstFeedbackAlignForwardRequest());

            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.BoundaryEffectRequested), "ALIGN qualifies on the flipped parity");
            Assert.AreEqual(0, CountNearMissReason(record, "PARITY:ODD"));
            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionTargetLocked));
            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(6), record.FinalState.FinalScore);
        }

        [Test]
        public void BurstOutput_InverseParityFlip_EmitsExactlyOneOddNearMissWithAllowanceIntact()
        {
            // Pre-Burst Value is odd (1) — ALIGN qualifies in the discarded creator-offer batch; the
            // Burst branch's FEEDBACK flips it to even (2), so ALIGN's fresh offer near-misses exactly
            // once and never commits.
            ExecutionRecord record = Execute(BurstFeedbackAlignInverseRequest());

            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.BoundaryEffectRequested), "no ALIGN commitment");
            Assert.AreEqual(1, CountNearMissReason(record, "PARITY:ODD"), "exactly one near-miss across the whole boundary");
            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionTargetLocked));
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(6), record.FinalState.FinalScore);
        }

        [Test]
        public void TwoBurstInstances_HoldIndependentLocks_AndCommuteTwoRequestsAgainstTheHost()
        {
            // Two BURST instances each lock the one Score host and each re-execute it: Score 0 → 3 →
            // 6 → 9, two tokens, two requests, and neither lock re-commits inside the other's branch.
            ExecutionRecord record = Execute(TwoBurstRequest());

            Assert.AreEqual(2, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionTargetLocked));
            Assert.AreEqual(1, CountRequestsOf(record, new InstanceID(900)));
            Assert.AreEqual(1, CountRequestsOf(record, new InstanceID(901)));
            Assert.AreEqual(3, record.Units.Count);
            Assert.AreEqual(new ScoreValue(9), record.FinalState.FinalScore);
            Assert.AreEqual(1, CountChainEvents(record, ExecutionEventSubtypes.EffectCommitted, new InstanceID(900)),
                "the second instance's lock survives the first branch — no re-commit");
            Assert.AreEqual(1, CountChainEvents(record, ExecutionEventSubtypes.EffectCommitted, new InstanceID(901)));

            List<EventEvidence> requests = RequestsOf(record);
            for (int i = 0; i < requests.Count; i++)
            {
                Assert.AreEqual(new InstanceID(10), requests[i].HostInstance, "both requests lock the same host");
            }
        }

        [Test]
        public void BurstOutput_LockedHostInSucceedingCondition_DeliversRetainedTrueAtTheBurstDescendant()
        {
            // The locked host is a TRUE-PATCH-carrying Score host inside a succeeding Condition. The
            // Burst descendant re-offers the retained TRUE outcome at its closure, so TRUE PATCH fires
            // there too — and the nested descendant proves the per-activation frame is self-rooted.
            ExecutionRecord record = Execute(BurstConditionRetainedTrueRequest());

            Assert.AreEqual(2, CountRequestsOf(record, new InstanceID(60)),
                "TRUE PATCH fires from its own closure and from the Burst descendant carrying retained TRUE");
            Assert.AreEqual(1, CountRequestsOf(record, new InstanceID(900)));
            Assert.AreEqual(new ScoreValue(8), record.FinalState.FinalScore);
        }

        [Test]
        public void BurstOutput_RescuedFromFalseLockedHost_DeliversRetainedFalseAtTheBurstDescendant()
        {
            // The rescued-from-FALSE locked host carries the retained FALSE outcome into its Burst
            // descendant: TRUE PATCH near-misses at both closures, never derived from the (present)
            // evaluation identity.
            ExecutionRecord record = Execute(BurstConditionRetainedFalseRequest());

            Assert.AreEqual(0, CountRequestsOf(record, new InstanceID(60)));
            Assert.AreEqual(2, CountNearMissReason(record, "STRUCTURE_CONTEXT:INSIDE_SUCCEEDING_CONDITION"),
                "the canonical closure and the Burst descendant both carry retained FALSE");
            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionTargetLocked));
            Assert.AreEqual(new ScoreValue(4), record.FinalState.FinalScore);
        }

        [Test]
        public void BurstWithAlign_DoubleRun_IsStructurallyEqual()
        {
            ExecutionRecord first = Execute(BurstFeedbackAlignForwardRequest());
            ExecutionRecord second = Execute(BurstFeedbackAlignForwardRequest());

            Assert.AreEqual(first.Events.Count, second.Events.Count);
            Assert.AreEqual(first.Units.Count, second.Units.Count);
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
        /// Returns every added-execution request event in the record, in order.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The request events.</returns>
        private static List<EventEvidence> RequestsOf(ExecutionRecord record)
        {
            List<EventEvidence> matches = new List<EventEvidence>();
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype == ExecutionEventSubtypes.AddedExecutionRequested)
                    matches.Add(evidence);
            }

            return matches;
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
        /// Counts the record-wide chain events of the subtype a named effect produced.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <param name="origin">The effect's origin instance.</param>
        /// <returns>The count.</returns>
        private static int CountChainEvents(ExecutionRecord record, string subtype, InstanceID origin)
        {
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype == subtype && evidence.EffectOriginInstance == origin)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// One player <c>Score += 3</c> host under BURST OUTPUT alone.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest BurstRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), UnpatchedHost(10, ScoreAddOperation(3)))
            });

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), BurstOnly(900));
        }

        /// <summary>
        /// Two Score hosts — id 20 at position 1, id 10 at position 2 — so the lock overwrites to the
        /// most recent qualifying host (id 10).
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest BurstOverwriteRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), UnpatchedHost(20, ScoreAddOperation(2))),
                SourceSlot.ForInstruction(new SourcePosition(2), UnpatchedHost(10, ScoreAddOperation(5)))
            });

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), BurstOnly(900));
        }

        /// <summary>
        /// A Value-only player host under BURST OUTPUT: no player Score gain, so no lock is held.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest BurstNoScoreRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(10, 2))
            });

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), BurstOnly(900));
        }

        /// <summary>
        /// The forward parity-flip fixture: the FEEDBACK-patched Score host (id 10) plus a plain
        /// <c>Value += 1</c> host (id 20) make pre-Burst Value even (2); the Burst branch's FEEDBACK
        /// gain flips it to odd (3) so ALIGN's fresh offer qualifies.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest BurstFeedbackAlignForwardRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), PatchedHost(10, 60, "WB-PAT-005", FeedbackPatchEffect(), ScoreAddOperation(3))),
                SourceSlot.ForInstruction(new SourcePosition(2), SchedulerFixtures.ValueAddInstance(20, 1))
            });

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), FeedbackBurstAlignPragmas());
        }

        /// <summary>
        /// The inverse parity-flip fixture: the FEEDBACK-patched Score host (id 10) alone makes
        /// pre-Burst Value odd (1) — ALIGN qualifies in the discarded creator offer — and the Burst
        /// branch flips it to even (2), so ALIGN's fresh offer near-misses.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest BurstFeedbackAlignInverseRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), PatchedHost(10, 60, "WB-PAT-005", FeedbackPatchEffect(), ScoreAddOperation(3)))
            });

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), FeedbackBurstAlignPragmas());
        }

        /// <summary>
        /// Two BURST OUTPUT instances (ids 900, 901) over one Score host.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest TwoBurstRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), UnpatchedHost(10, ScoreAddOperation(3)))
            });
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>
            {
                BurstOutputPragma(900),
                BurstOutputPragma(901)
            };

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), pragmas);
        }

        /// <summary>
        /// A TRUE-PATCH-carrying Score host inside a succeeding Condition, locked by BURST OUTPUT: the
        /// retained TRUE outcome must reach the Burst descendant's closure.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest BurstConditionRetainedTrueRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance condition = SchedulerFixtures.ConditionStructure(20, CoreRegister.Value, PredicateComparison.AtLeast, 1, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignValueCoreLine("core-01", 1)),
                SourceSlot.ForStructureHeader(new SourcePosition(2), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(3), condition, PatchedHost(10, 60, "WB-PAT-004", TruePatchEffect(), ScoreAddOperation(2)))
            });

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, new List<DependencyInstance>(), BurstOnly(900));
        }

        /// <summary>
        /// A rescued-from-FALSE TRUE-PATCH-carrying Score host, locked by BURST OUTPUT: the retained
        /// FALSE outcome must reach the Burst descendant's closure.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest BurstConditionRetainedFalseRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance condition = SchedulerFixtures.ConditionStructure(20, CoreRegister.Value, PredicateComparison.AtLeast, 3, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), condition, PatchedHost(10, 60, "WB-PAT-004", TruePatchEffect(), ScoreAddOperation(2)))
            });
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                SchedulerFixtures.SafeModeInstance(40)
            };

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, installed, BurstOnly(900));
        }

        /// <summary>
        /// The BURST OUTPUT pragma alone.
        /// </summary>
        /// <param name="instance">The pragma instance identity value.</param>
        /// <returns>The pragma list.</returns>
        private static List<DirectiveInstance> BurstOnly(int instance)
        {
            return new List<DirectiveInstance> { BurstOutputPragma(instance) };
        }

        /// <summary>
        /// The FEEDBACK-host fixture pragmas: BURST OUTPUT then ALIGN.
        /// </summary>
        /// <returns>The pragma list.</returns>
        private static List<DirectiveInstance> FeedbackBurstAlignPragmas()
        {
            return new List<DirectiveInstance>
            {
                BurstOutputPragma(900),
                SchedulerFixtures.AlignPragma(950)
            };
        }

        /// <summary>
        /// A BURST-OUTPUT-shaped Directive instance (WB-DIR-003, post-fix): a target-lock update that
        /// tracks the most recent qualifying player Score gain, and a boundary creator that
        /// re-executes the locked target once per execution with cancel-on-invalid.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Directive instance.</returns>
        private static DirectiveInstance BurstOutputPragma(int instance)
        {
            EffectTiming reactionTiming = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor lockTrigger = new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                    new TriggerQualifier("REGISTER", "SCORE"),
                    new TriggerQualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION")
                },
                reactionTiming);
            EffectDefinition lockEffect = new EffectDefinition(
                PhaseDomain.Execution,
                lockTrigger,
                new TargetLockUpdateOperation(new TargetingRule("MOST_RECENT_QUALIFYING_UNIT", string.Empty)),
                new TargetingRule("MOST_RECENT_QUALIFYING_UNIT", string.Empty),
                reactionTiming,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));

            EffectTiming boundaryTiming = new EffectTiming(TimingKind.NamedBoundary, "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL");
            TriggerDescriptor creatorTrigger = new TriggerDescriptor(
                EventFamily.Reaction,
                "BOUNDARY_EFFECT_REQUESTED",
                new List<TriggerQualifier>(),
                boundaryTiming);
            EffectDefinition creatorEffect = new EffectDefinition(
                PhaseDomain.Execution,
                creatorTrigger,
                new AddedExecutionRequestOperation(new TargetingRule("LOCKED_TARGET", string.Empty), true),
                new TargetingRule("LOCKED_TARGET", string.Empty),
                boundaryTiming,
                StackingMode.IndependentResolution,
                new EffectFrequency("ONCE", "EXECUTION"));

            DirectiveDefinition definition = new DirectiveDefinition(
                new DirectiveID("WB-DIR-003"),
                "Test rules.",
                "TEST DIRECTIVE",
                ContentCategory.Directive,
                Rarity.Uncommon,
                new List<string>(),
                new List<EffectDefinition> { lockEffect, creatorEffect });

            return new DirectiveInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// A FEEDBACK-PATCH-shaped socketed effect: the host's own positive Score change adds 1 to
        /// Value at the immediate-reaction band.
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
        /// A TRUE-PATCH-shaped socketed effect: the succeeding-Condition host closure creates one
        /// own-host descendant, once per source execution.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition TruePatchEffect()
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Lifecycle,
                "RUNTIME_UNIT_COMPLETED",
                new List<TriggerQualifier> { new TriggerQualifier("STRUCTURE_CONTEXT", "INSIDE_SUCCEEDING_CONDITION") },
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
        /// <returns>The Instruction instance.</returns>
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
        /// A fixed <c>Score += constant</c> primary operation.
        /// </summary>
        /// <param name="constant">The fixed operand.</param>
        /// <returns>The operation.</returns>
        private static QuantityChangeOperation ScoreAddOperation(int constant)
        {
            return new QuantityChangeOperation(CoreRegister.Score, QuantityOperator.Add, OperandSpec.FromConstant(constant));
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
    }
}
