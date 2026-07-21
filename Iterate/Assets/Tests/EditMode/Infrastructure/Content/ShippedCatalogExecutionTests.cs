using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using Iterate.Infrastructure.Content;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// The Epic's first end-to-end evidence: the real frozen catalog loaded through the standing
    /// <see cref="CatalogDirectorySource"/> path, the Design §28.3 Tutorial Process 1 Core assembled with
    /// real shipped Instructions, executed through the <see cref="ExecutionScheduler"/>. The expected
    /// Scores are the hand-traced skeleton-scope values (10 / 6 / 20 / 12 / 32); §28.3's published numbers
    /// (12 / 7 / 24 / 14 / 26 / 36) include STANDARD LIBRARY's first-eligible <c>+1</c>, which is child iv's
    /// scope — no Dependencies exist in these fixtures — and become assertable there.
    /// </summary>
    public sealed class ShippedCatalogExecutionTests
    {
        private ContentCatalog _catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            _catalog = Load();
        }

        [Test]
        public void Execution1Strong_ScoresTen_NoCrossings()
        {
            InstanceIDSource ids = new InstanceIDSource();
            ExecutionRecord record = Execute(BuildRequest(new InstructionInstance[]
            {
                Instruction("WB-INS-002", ids),
                Instruction("WB-INS-002", ids),
                Instruction("WB-INS-012", ids),
                null,
                null
            }));

            Assert.AreEqual(new ScoreValue(10), record.FinalState.FinalOutput);
            Assert.AreEqual(0, record.ThresholdHistory.Count);
            AssertHealthyRun(record);
        }

        [Test]
        public void Execution1Weak_ScoresSix_NoCrossings()
        {
            InstanceIDSource ids = new InstanceIDSource();
            ExecutionRecord record = Execute(BuildRequest(new InstructionInstance[]
            {
                Instruction("WB-INS-012", ids),
                Instruction("WB-INS-002", ids),
                Instruction("WB-INS-002", ids),
                null,
                null
            }));

            Assert.AreEqual(new ScoreValue(6), record.FinalState.FinalOutput);
            Assert.AreEqual(0, record.ThresholdHistory.Count);
            AssertHealthyRun(record);
        }

        [Test]
        public void Execution2Strong_ScoresTwenty()
        {
            InstanceIDSource ids = new InstanceIDSource();
            ExecutionRecord record = Execute(BuildRequest(new InstructionInstance[]
            {
                Instruction("WB-INS-002", ids),
                Instruction("WB-INS-002", ids),
                Instruction("WB-INS-005", ids),
                Instruction("WB-INS-012", ids),
                null
            }));

            Assert.AreEqual(new ScoreValue(20), record.FinalState.FinalOutput);
            AssertHealthyRun(record);
        }

        [Test]
        public void Execution2Weak_ScoresTwelve()
        {
            InstanceIDSource ids = new InstanceIDSource();
            ExecutionRecord record = Execute(BuildRequest(new InstructionInstance[]
            {
                Instruction("WB-INS-005", ids),
                Instruction("WB-INS-002", ids),
                Instruction("WB-INS-002", ids),
                Instruction("WB-INS-012", ids),
                null
            }));

            Assert.AreEqual(new ScoreValue(12), record.FinalState.FinalOutput);
            AssertHealthyRun(record);
        }

        [Test]
        public void Execution3Maximum_ScoresThirtyTwo_CrossesPassThenOptimize()
        {
            InstanceIDSource ids = new InstanceIDSource();
            ExecutionRecord record = Execute(BuildRequest(new InstructionInstance[]
            {
                Instruction("WB-INS-002", ids),
                Instruction("WB-INS-002", ids),
                Instruction("WB-INS-003", ids),
                Instruction("WB-INS-005", ids),
                Instruction("WB-INS-012", ids)
            }));

            Assert.AreEqual(new ScoreValue(32), record.FinalState.FinalOutput);
            Assert.AreEqual(2, record.ThresholdHistory.Count);

            EventEvidence pass = FindEvent(record, record.ThresholdHistory[0]);
            EventEvidence optimize = FindEvent(record, record.ThresholdHistory[1]);
            Assert.AreEqual(ProcessThresholds.PassName, pass.TargetIdentity);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, pass.Subtype);
            Assert.AreEqual(ProcessThresholds.OptimizeName, optimize.TargetIdentity);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, optimize.Subtype);

            for (int i = 0; i < record.ThresholdHistory.Count; i++)
                Assert.AreNotEqual(ProcessThresholds.BenchmarkName, FindEvent(record, record.ThresholdHistory[i]).TargetIdentity);

            AssertHealthyRun(record);
        }

        [Test]
        public void RepeatedScenarioOnOneBuilder_YieldsStructurallyEqualRecords()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            InstanceIDSource ids = new InstanceIDSource();
            ExecutionRequest request = BuildRequest(new InstructionInstance[]
            {
                Instruction("WB-INS-002", ids),
                Instruction("WB-INS-002", ids),
                Instruction("WB-INS-012", ids),
                null,
                null
            });

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(first, second);
        }

        private InstructionInstance Instruction(string id, InstanceIDSource ids)
        {
            Assert.IsTrue(_catalog.TryGetInstruction(new InstructionID(id), out InstructionDefinition definition), id);
            return new InstructionInstance(ids.Next(), definition, null);
        }

        private static ExecutionRequest BuildRequest(InstructionInstance[] players)
        {
            CoreLine coreOne = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            CoreLine coreSeven = new CoreLine("core-07", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));

            List<SourceSlot> slots = new List<SourceSlot>();
            slots.Add(SourceSlot.ForCore(new SourcePosition(1), coreOne));
            for (int i = 0; i < players.Length; i++)
            {
                int position = i + 2;
                if (players[i] == null)
                    slots.Add(SourceSlot.ForEmpty(new SourcePosition(position)));
                else
                    slots.Add(SourceSlot.ForInstruction(new SourcePosition(position), players[i]));
            }

            slots.Add(SourceSlot.ForCore(new SourcePosition(7), coreSeven));

            SourceArrangement arrangement = new SourceArrangement(slots);
            CompiledSource source = new CompiledSource(arrangement, new List<DirectiveInstance>(), StandardCost());

            return new ExecutionRequest(source, StandardConfiguration(), StandardStamps(), ZeroState());
        }

        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        private static void AssertHealthyRun(ExecutionRecord record)
        {
            Assert.AreEqual(ExecutionCompletionStatus.Completed, record.CompletionStatus);
            Assert.AreEqual(ResultValidityStatus.Valid, record.ResultValidity);
            Assert.AreEqual(ResultReviewHandoffStatus.Eligible, record.HandoffStatus);
            Assert.AreEqual(SafetyStatus.Normal, record.SafetyStatus);
            Assert.LessOrEqual(record.SafetyCounts.SourceExecutionUnits, 7);
        }

        private static EventEvidence FindEvent(ExecutionRecord record, TraceEventID identity)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Identity == identity)
                    return record.Events[i].Evidence;
            }

            throw new InvalidOperationException("The referenced event was not found in the record.");
        }

        private static ProcessExecutionConfiguration StandardConfiguration()
        {
            return new ProcessExecutionConfiguration(
                "exec",
                "compilation",
                "source-rev",
                "process",
                "core",
                "rule-config",
                "session-seed",
                new ProcessThresholds(new ScoreValue(20), new ScoreValue(30), new ScoreValue(36)));
        }

        private static List<RevisionStamp> StandardStamps()
        {
            return new List<RevisionStamp>
            {
                new RevisionStamp("Content Catalog", "0.1.0"),
                new RevisionStamp("Random Service", "iterate-rng-1")
            };
        }

        private static InitialExecutionState ZeroState()
        {
            return new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0));
        }

        private static CompilationCostBreakdown StandardCost()
        {
            return new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false);
        }

        private static ContentCatalog Load()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Catalog");
            CatalogDirectorySource source = new(root);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            return loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
