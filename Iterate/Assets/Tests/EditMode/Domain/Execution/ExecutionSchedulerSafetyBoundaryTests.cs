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
    /// Tests the two added-execution ceilings and multi-limit recording end to end through the
    /// scheduler's entry point — CAB-SAFE-153, CAB-SAFE-154 and CAB-SAFE-158.
    /// Every fixture here is driven by one lever: the number of distinct every-qualifying creator
    /// instances. A creator's origin is its <em>instance</em> identity, every request appends its
    /// creator's origin to the branch lineage, and every creator match path refuses an effect whose
    /// origin already sits in that lineage — the origin lock. So one creator instance chains exactly
    /// one level, and a chain of N creators descends at most N deep, with N minus the current depth
    /// still unlocked at each level. The drain is depth-first, because a unit drains its own
    /// captured batch inside its own traversal before its frame pops.
    /// That gives two cleanly separated boundaries from the same shape. Thirty-three creators
    /// descend the first branch to depth 32, where exactly one creator remains unlocked and requests
    /// depth 33 — the lineage-depth breach, costing 33 units. Six creators self-terminate at depth
    /// six but attempt 1,956 descendants under the one root, so descendant 1,025 breaches the
    /// per-activation ceiling long before depth or units are in question.
    /// The multi-limit case pairs them: a filler Repeat of non-qualifying units placed ahead of a
    /// six-creator root puts the execution's unit count at exactly its ceiling as descendant 1,025
    /// is attempted, so one attempted occurrence exceeds two ceilings and records both.
    /// The large fixtures run once in one-time setup and are shared across their assertions.
    /// </summary>
    public sealed class ExecutionSchedulerSafetyBoundaryTests
    {
        /// <summary>
        /// The creator count whose chain attempts one level past the lineage-depth ceiling.
        /// </summary>
        private const int DepthBreachingCreators = SafetyCeilings.AddedExecutionLineageDepth + 1;

        /// <summary>
        /// The creator count whose fan-out attempts more descendants under one root than the
        /// per-activation ceiling permits, while self-terminating far below the depth ceiling.
        /// </summary>
        private const int DescendantBreachingCreators = 6;

        /// <summary>
        /// The creator count whose fan-out stays under the per-activation ceiling per root while
        /// four roots together exceed it — 325 descendants each, 1,300 in total.
        /// </summary>
        private const int SeparateRootCreators = 5;

        /// <summary>
        /// The descendants one <see cref="SeparateRootCreators"/> fan-out attempts under one root.
        /// </summary>
        private const int DescendantsPerSeparateRoot = 325;

        /// <summary>
        /// The number of roots the separate-counts fixture activates.
        /// </summary>
        private const int SeparateRoots = 4;

        /// <summary>
        /// The record of the 33-creator chain that breaches the lineage-depth ceiling.
        /// </summary>
        private ExecutionRecord _depthRecord;

        /// <summary>
        /// The record of the six-creator fan-out that breaches the per-activation ceiling.
        /// </summary>
        private ExecutionRecord _descendantRecord;

        /// <summary>
        /// The record of the four-root fan-out that stays legal under separate counts.
        /// </summary>
        private ExecutionRecord _separateRootsRecord;

        /// <summary>
        /// The record whose single attempted descendant breaches two ceilings at once.
        /// </summary>
        private ExecutionRecord _multiLimitRecord;

        /// <summary>
        /// Runs the four shared fixtures once, so the largest of them executes a single time for the
        /// whole suite.
        /// </summary>
        [OneTimeSetUp]
        public void RunSharedFixtures()
        {
            _depthRecord = Execute(CreatorChainRequest(DepthBreachingCreators));
            _descendantRecord = Execute(CreatorChainRequest(DescendantBreachingCreators));
            _separateRootsRecord = Execute(SeparateRootsRequest());
            _multiLimitRecord = Execute(MultiLimitRequest());
        }

        [Test]
        public void DepthChain_PermitsDepth32()
        {
            Assert.AreEqual(
                SafetyCeilings.AddedExecutionLineageDepth,
                _depthRecord.SafetyCounts.LineageDepthHighWater);
            Assert.AreEqual(
                SafetyCeilings.AddedExecutionLineageDepth,
                _depthRecord.SafetyCounts.AddedDescendants);
        }

        [Test]
        public void DepthChain_AttemptedDepth33_TriggersSafetyIntervention()
        {
            EventEvidence reached = SingleSubtype(_depthRecord, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            Assert.AreEqual(ExecutionCompletionStatus.SafetyAborted, _depthRecord.CompletionStatus);
            Assert.AreEqual(SafetyStatus.SafetyAborted, _depthRecord.SafetyStatus);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.AddedExecutionLineageDepth),
                payload.BreachedLimits[0].LimitName);
            Assert.AreEqual(SafetyCeilings.AddedExecutionLineageDepth, payload.BreachedLimits[0].Ceiling);
            Assert.AreEqual(
                SafetyCeilings.AddedExecutionLineageDepth + 1,
                payload.BreachedLimits[0].CountAtBreach);
        }

        [Test]
        public void DepthChain_BreachNamesOnlyTheDepthLimit()
        {
            EventEvidence reached = SingleSubtype(_depthRecord, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            Assert.AreEqual(1, payload.BreachedLimits.Count);
        }

        [Test]
        public void DepthChain_OverLimitSourceExecutionNeverResolves()
        {
            Assert.AreEqual(DepthBreachingCreators, _depthRecord.Units.Count);
            Assert.AreEqual(DepthBreachingCreators, _depthRecord.SafetyCounts.SourceExecutionUnits);
            Assert.AreEqual(
                SafetyCeilings.AddedExecutionLineageDepth,
                CountSubtype(_depthRecord, ExecutionEventSubtypes.AddedExecutionStarted));
            Assert.AreEqual(0, CountSubtype(_depthRecord, ExecutionEventSubtypes.AddedExecutionCompleted));
        }

        [Test]
        public void DepthChain_ExecutionBecomesInvalid()
        {
            Assert.AreEqual(ResultValidityStatus.Invalid, _depthRecord.ResultValidity);
            Assert.AreEqual(ResultReviewHandoffStatus.Blocked, _depthRecord.HandoffStatus);
            Assert.AreEqual(0, CountSubtype(_depthRecord, ExecutionEventSubtypes.ExecutionCompleted));
            Assert.AreEqual(1, CountSubtype(_depthRecord, ExecutionEventSubtypes.ExecutionSafetyAborted));
        }

        [Test]
        public void DepthChain_AbortIdentityNamesTheAddedExecutionStart()
        {
            EventEvidence reached = SingleSubtype(_depthRecord, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            StringAssert.StartsWith(
                SafetyAbortSignal.ComposeOccurrenceIdentity(
                    EventFamilies.AddedExecution,
                    ExecutionEventSubtypes.AddedExecutionStarted,
                    string.Empty),
                payload.OverLimitOccurrenceIdentity);
        }

        [Test]
        public void ShallowCreatorChain_CompletesNormallyWellInsideEveryCeiling()
        {
            ExecutionRecord record = Execute(CreatorChainRequest(3));

            Assert.AreEqual(ExecutionCompletionStatus.Completed, record.CompletionStatus);
            Assert.AreEqual(SafetyStatus.Normal, record.SafetyStatus);
            Assert.AreEqual(3, record.SafetyCounts.LineageDepthHighWater);
            Assert.AreEqual(15, record.SafetyCounts.AddedDescendants);
            Assert.AreEqual(0, CountFamily(record, EventFamilies.Safety));
        }

        [Test]
        public void DescendantFanOut_Permits1024DescendantsForOneRoot()
        {
            Assert.AreEqual(
                SafetyCeilings.AddedExecutionsPerActivation,
                _descendantRecord.SafetyCounts.AddedDescendants);
            Assert.AreEqual(
                SafetyCeilings.AddedExecutionsPerActivation + 1,
                _descendantRecord.Units.Count);
        }

        [Test]
        public void DescendantFanOut_Attempted1025_TriggersSafetyIntervention()
        {
            EventEvidence reached = SingleSubtype(_descendantRecord, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            Assert.AreEqual(ExecutionCompletionStatus.SafetyAborted, _descendantRecord.CompletionStatus);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.AddedExecutionsPerActivation),
                payload.BreachedLimits[0].LimitName);
            Assert.AreEqual(
                SafetyCeilings.AddedExecutionsPerActivation,
                payload.BreachedLimits[0].Ceiling);
            Assert.AreEqual(
                SafetyCeilings.AddedExecutionsPerActivation,
                payload.BreachedLimits[0].CountAtBreach);
        }

        [Test]
        public void DescendantFanOut_BreachesOnlyThePerActivationLimit()
        {
            EventEvidence reached = SingleSubtype(_descendantRecord, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            Assert.AreEqual(1, payload.BreachedLimits.Count);
            Assert.AreEqual(DescendantBreachingCreators, _descendantRecord.SafetyCounts.LineageDepthHighWater);
            Assert.Less(
                _descendantRecord.SafetyCounts.SourceExecutionUnits,
                SafetyCeilings.SourceExecutionUnitsPerExecution);
        }

        [Test]
        public void SeparateRootActivations_MaintainSeparateDescendantCounts()
        {
            Assert.AreEqual(ExecutionCompletionStatus.Completed, _separateRootsRecord.CompletionStatus);
            Assert.AreEqual(SafetyStatus.Normal, _separateRootsRecord.SafetyStatus);
            Assert.AreEqual(
                SeparateRoots * DescendantsPerSeparateRoot,
                _separateRootsRecord.SafetyCounts.AddedDescendants);
            Assert.AreEqual(
                SeparateRoots + (SeparateRoots * DescendantsPerSeparateRoot),
                _separateRootsRecord.Units.Count);
        }

        [Test]
        public void SeparateRootDescendantTotal_WouldBreachASharedCounter()
        {
            Assert.Greater(
                _separateRootsRecord.SafetyCounts.AddedDescendants,
                SafetyCeilings.AddedExecutionsPerActivation);
            Assert.AreEqual(0, CountFamily(_separateRootsRecord, EventFamilies.Safety));
        }

        [Test]
        public void MultiLimitBreach_RecordsEveryAffectedLimit()
        {
            EventEvidence reached = SingleSubtype(_multiLimitRecord, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            Assert.AreEqual(ExecutionCompletionStatus.SafetyAborted, _multiLimitRecord.CompletionStatus);
            Assert.AreEqual(2, payload.BreachedLimits.Count);
        }

        [Test]
        public void MultiLimitBreach_ListsBothLimitsInRegistryOrder()
        {
            EventEvidence reached = SingleSubtype(_multiLimitRecord, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.AddedExecutionsPerActivation),
                payload.BreachedLimits[0].LimitName);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.SourceExecutionUnits),
                payload.BreachedLimits[1].LimitName);
        }

        [Test]
        public void MultiLimitBreach_CarriesEachLimitsOwnCeilingAndCount()
        {
            EventEvidence reached = SingleSubtype(_multiLimitRecord, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            Assert.AreEqual(
                SafetyCeilings.AddedExecutionsPerActivation,
                payload.BreachedLimits[0].Ceiling);
            Assert.AreEqual(
                SafetyCeilings.AddedExecutionsPerActivation,
                payload.BreachedLimits[0].CountAtBreach);
            Assert.AreEqual(
                SafetyCeilings.SourceExecutionUnitsPerExecution,
                payload.BreachedLimits[1].Ceiling);
            Assert.AreEqual(
                SafetyCeilings.SourceExecutionUnitsPerExecution,
                payload.BreachedLimits[1].CountAtBreach);
        }

        [Test]
        public void MultiLimitBreach_IsDeterministicAcrossTwoRuns()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = MultiLimitRequest();

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// One top-level <c>Value += 1</c> Instruction under the given number of distinct
        /// every-qualifying creator Directive instances. Each execution of the Instruction raises
        /// Value, so every creator whose origin is not yet in the branch lineage requests one added
        /// execution of the triggering unit.
        /// </summary>
        /// <param name="creatorCount">The number of distinct creator instances to install.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest CreatorChainRequest(int creatorCount)
        {
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(50, 1))
            });

            return SchedulerFixtures.RequestOver(
                arrangement,
                SchedulerFixtures.ZeroState(),
                new InstanceIDSource(),
                new List<DependencyInstance>(),
                Creators(creatorCount));
        }

        /// <summary>
        /// Four top-level <c>Value += 1</c> Instructions under five creator instances, so each root
        /// activation fans out 325 descendants of its own and the four together exceed the
        /// per-activation ceiling that none of them individually approaches.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest SeparateRootsRequest()
        {
            List<SourceSlot> slots = new List<SourceSlot>();
            for (int i = 0; i < SeparateRoots; i++)
            {
                slots.Add(SourceSlot.ForInstruction(
                    new SourcePosition(i + 1),
                    SchedulerFixtures.ValueAddInstance(50 + i, 1)));
            }

            return SchedulerFixtures.RequestOver(
                new SourceArrangement(slots),
                SchedulerFixtures.ZeroState(),
                new InstanceIDSource(),
                new List<DependencyInstance>(),
                Creators(SeparateRootCreators));
        }

        /// <summary>
        /// A filler Repeat of non-qualifying <c>Signal += 1</c> units ahead of a six-creator
        /// <c>Value += 1</c> root. The filler count is chosen so the execution's unit count reaches
        /// its ceiling exactly as the root's descendant count reaches its own: the filler units, the
        /// root, and its 1,024 permitted descendants sum to the unit ceiling, so the attempt at
        /// descendant 1,025 exceeds both at once.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest MultiLimitRequest()
        {
            int fillerUnits =
                SafetyCeilings.SourceExecutionUnitsPerExecution
                - 1
                - SafetyCeilings.AddedExecutionsPerActivation;
            StructureInstance filler = SchedulerFixtures.RepeatStructure(60, fillerUnits, 2);

            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), filler),
                SourceSlot.ForContainedInstruction(
                    new SourcePosition(2),
                    filler,
                    SchedulerFixtures.SignalAddInstance(61, 1)),
                SourceSlot.ForInstruction(new SourcePosition(3), SchedulerFixtures.ValueAddInstance(50, 1))
            });

            return SchedulerFixtures.RequestOver(
                arrangement,
                SchedulerFixtures.ZeroState(),
                new InstanceIDSource(),
                new List<DependencyInstance>(),
                Creators(DescendantBreachingCreators));
        }

        /// <summary>
        /// The given number of every-qualifying creator Directive instances, each on its own
        /// instance identity so each carries a distinct origin the lineage locks independently.
        /// </summary>
        /// <param name="count">The number of instances to build.</param>
        /// <returns>The Directive instances.</returns>
        private static List<DirectiveInstance> Creators(int count)
        {
            List<DirectiveInstance> creators = new List<DirectiveInstance>();
            for (int i = 0; i < count; i++)
            {
                creators.Add(SchedulerFixtures.EveryQualifyingCreatorPragma(100 + i));
            }

            return creators;
        }

        /// <summary>
        /// Runs one request on a fresh scheduler.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <returns>The frozen record.</returns>
        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        /// <summary>
        /// Counts the events carrying one subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype to count.</param>
        /// <returns>The number of matching events.</returns>
        private static int CountSubtype(ExecutionRecord record, string subtype)
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
        /// Counts the events carrying one family.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="family">The family to count.</param>
        /// <returns>The number of matching events.</returns>
        private static int CountFamily(ExecutionRecord record, string family)
        {
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Family == family)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// The single event carrying one subtype, asserting exactly one exists.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype to find.</param>
        /// <returns>The matching evidence.</returns>
        private static EventEvidence SingleSubtype(ExecutionRecord record, string subtype)
        {
            int found = -1;
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype != subtype)
                    continue;

                if (found < 0)
                    found = i;

                count++;
            }

            Assert.AreEqual(1, count);
            return record.Events[found].Evidence;
        }
    }
}
