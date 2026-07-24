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
    /// Tests that branch lineage is populated exactly where added executions exist and nowhere else:
    /// canonical arrangements keep the empty lineage on every event and opening, so child iii–v
    /// evidence is unchanged, and executions carrying creators double-run structurally equal.
    /// </summary>
    public sealed class ExecutionSchedulerLineageTests
    {
        [Test]
        public void CanonicalArrangement_EveryEvent_CarriesTheEmptyLineage()
        {
            ExecutionRecord record = Execute(CanonicalRequest());

            for (int i = 0; i < record.Events.Count; i++)
            {
                Assert.AreEqual(
                    EffectOriginLineage.Empty,
                    record.Events[i].Evidence.Lineage,
                    record.Events[i].Evidence.Subtype);
            }
        }

        [Test]
        public void CanonicalArrangement_EveryOpening_IsCanonicalAndSelfRooted()
        {
            ExecutionRecord record = Execute(CanonicalRequest());

            for (int i = 0; i < record.Units.Count; i++)
            {
                RuntimeUnitOpening opening = record.Units[i].Opening;
                Assert.AreEqual(ActivationKind.CanonicalTraversal, opening.Activation);
                Assert.AreEqual(EffectOriginLineage.Empty, opening.Lineage);
                Assert.AreEqual(0, opening.AddedExecutionDepth);
                Assert.IsNull(opening.ParentUnit);
                Assert.IsNull(opening.OriginatingRequestIdentity);
            }
        }

        [Test]
        public void CanonicalArrangement_SafetyCounts_ReportNoAddedExecutions()
        {
            ExecutionRecord record = Execute(CanonicalRequest());

            Assert.AreEqual(0, record.SafetyCounts.LineageDepthHighWater);
            Assert.AreEqual(0, record.SafetyCounts.AddedDescendants);
        }

        [Test]
        public void CanonicalArrangement_DoubleRun_IsStructurallyEqual()
        {
            ExecutionRecord first = Execute(CanonicalRequest());
            ExecutionRecord second = Execute(CanonicalRequest());

            Assert.AreEqual(first.Events.Count, second.Events.Count);
            Assert.AreEqual(first.Units.Count, second.Units.Count);
            Assert.AreEqual(first.FinalState, second.FinalState);
            Assert.AreEqual(first.SafetyCounts, second.SafetyCounts);
        }

        [Test]
        public void CreatorArrangement_DoubleRun_IsStructurallyEqual()
        {
            ExecutionRecord first = Execute(CreatorRequest());
            ExecutionRecord second = Execute(CreatorRequest());

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

        [Test]
        public void CreatorArrangement_CausingUnitEvents_KeepTheEmptyLineage()
        {
            ExecutionRecord record = Execute(CreatorRequest());
            RuntimeUnitRecord causing = record.Units[0];

            for (int i = 0; i < causing.ChildEvents.Count; i++)
            {
                EventEvidence evidence = record.Events[causing.ChildEvents[i].Value - 1].Evidence;
                if (evidence.Subtype == ExecutionEventSubtypes.AddedExecutionRequested)
                    continue;

                Assert.AreEqual(EffectOriginLineage.Empty, evidence.Lineage, evidence.Subtype);
            }
        }

        [Test]
        public void CreatorArrangement_RequestEvent_CarriesTheRequestLineage()
        {
            ExecutionRecord record = Execute(CreatorRequest());
            EffectOriginLineage expected = EffectOriginLineage.Empty.Append(new InstanceID(900));

            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype != ExecutionEventSubtypes.AddedExecutionRequested)
                    continue;

                Assert.AreEqual(expected, evidence.Lineage);
                return;
            }

            Assert.Fail("No request event exists.");
        }

        /// <summary>
        /// A Structure-bearing canonical arrangement with an installed reaction and modification but
        /// no creator: a Core line, a Repeat over a Signal-adding child, and a trailing player
        /// Instruction.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest CanonicalRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            CoreLine core = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            StructureInstance repeat = SchedulerFixtures.RepeatStructure(20, 2, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), core),
                SourceSlot.ForStructureHeader(new SourcePosition(2), repeat),
                SourceSlot.ForContainedInstruction(new SourcePosition(3), repeat, SchedulerFixtures.SignalAddInstance(10, 1)),
                SourceSlot.ForInstruction(new SourcePosition(4), SchedulerFixtures.ValueAddInstance(11, 2))
            });
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                SchedulerFixtures.StandardLibraryInstance(300),
                SchedulerFixtures.ParallelChannelInstance(400)
            };

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, installed);
        }

        /// <summary>
        /// One player <c>Value += 2</c> Instruction under the OVERCLOCK pragma — the minimal
        /// creator-bearing arrangement.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest CreatorRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(10, 2))
            });
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>
            {
                SchedulerFixtures.OverclockPragma(900)
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
    }
}
