using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Shared hand-built fixtures for the <see cref="ExecutionScheduler"/> tests: the Tutorial-Process-1
    /// shape (Core <c>Value = 1</c>, player Instructions, Core <c>Score += Value</c>) and a Pass-crossing
    /// arrangement, assembled with real <see cref="ExecutionRequest"/>/<see cref="CompiledSource"/> types.
    /// </summary>
    public static class SchedulerFixtures
    {
        /// <summary>
        /// The §28.3 Score bands: Pass 20, Optimize 30, Benchmark 36.
        /// </summary>
        /// <returns>The standard thresholds.</returns>
        public static ProcessThresholds StandardThresholds()
        {
            return new ProcessThresholds(new ScoreValue(20), new ScoreValue(30), new ScoreValue(36));
        }

        /// <summary>
        /// The standard two-stamp revision list: the content catalog and the determinism revision.
        /// </summary>
        /// <returns>The standard revision stamps.</returns>
        public static List<RevisionStamp> StandardStamps()
        {
            return new List<RevisionStamp>
            {
                new RevisionStamp("Content Catalog", "0.1.0"),
                new RevisionStamp("Random Service", "iterate-rng-1")
            };
        }

        /// <summary>
        /// The standard Process execution configuration carrying the seven header identities and the
        /// standard thresholds.
        /// </summary>
        /// <returns>The standard configuration.</returns>
        public static ProcessExecutionConfiguration StandardConfiguration()
        {
            return new ProcessExecutionConfiguration(
                "exec",
                "compilation",
                "source-rev",
                "process",
                "core",
                "rule-config",
                "session-seed",
                StandardThresholds());
        }

        /// <summary>
        /// An initial register state with all three registers zero.
        /// </summary>
        /// <returns>The zero initial state.</returns>
        public static InitialExecutionState ZeroState()
        {
            return new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0));
        }

        /// <summary>
        /// An initial register state seeded with the given register values.
        /// </summary>
        /// <param name="value">The initial Value register.</param>
        /// <param name="signal">The initial Signal register.</param>
        /// <param name="score">The initial Score register.</param>
        /// <returns>The seeded initial state.</returns>
        public static InitialExecutionState State(int value, int signal, int score)
        {
            return new InitialExecutionState(new ValueAmount(value), new SignalValue(signal), new ScoreValue(score));
        }

        /// <summary>
        /// The strong-order Tutorial-Process-1 request: Core <c>Value = 1</c> at 1, player <c>Value += 2</c>
        /// at 2 and 3, player <c>Score += Value</c> at 4, empties at 5 and 6, Core <c>Score += Value</c> at
        /// 7. Traverses to a final Score of 10 from a zero start.
        /// </summary>
        /// <param name="initialState">The initial register state.</param>
        /// <param name="ids">The instance-identity source.</param>
        /// <returns>The assembled request.</returns>
        public static ExecutionRequest StrongOrderRequest(InitialExecutionState initialState, InstanceIDSource ids)
        {
            CoreLine coreOne = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            CoreLine coreSeven = new CoreLine("core-07", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));

            InstructionInstance addA = new InstructionInstance(ids.Next(), AddValueInstruction(2), null);
            InstructionInstance addB = new InstructionInstance(ids.Next(), AddValueInstruction(2), null);
            InstructionInstance scoreAdd = new InstructionInstance(ids.Next(), ScoreAddValueInstruction(), null);

            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), coreOne),
                SourceSlot.ForInstruction(new SourcePosition(2), addA),
                SourceSlot.ForInstruction(new SourcePosition(3), addB),
                SourceSlot.ForInstruction(new SourcePosition(4), scoreAdd),
                SourceSlot.ForEmpty(new SourcePosition(5)),
                SourceSlot.ForEmpty(new SourcePosition(6)),
                SourceSlot.ForCore(new SourcePosition(7), coreSeven)
            });

            return BuildRequest(arrangement, initialState, ids);
        }

        /// <summary>
        /// A minimal request whose Score crosses Pass upward exactly once: Core <c>Value = 1</c> at 1,
        /// player <c>Value += 20</c> at 2, Core <c>Score += Value</c> at 3. From a zero start the Score
        /// event reads 0 → 21.
        /// </summary>
        /// <param name="ids">The instance-identity source.</param>
        /// <returns>The assembled request.</returns>
        public static ExecutionRequest PassCrossingRequest(InstanceIDSource ids)
        {
            CoreLine coreOne = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            CoreLine coreThree = new CoreLine("core-03", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));

            InstructionInstance boost = new InstructionInstance(ids.Next(), AddValueInstruction(20), null);

            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), coreOne),
                SourceSlot.ForInstruction(new SourcePosition(2), boost),
                SourceSlot.ForCore(new SourcePosition(3), coreThree)
            });

            return BuildRequest(arrangement, ZeroState(), ids);
        }

        private static ExecutionRequest BuildRequest(SourceArrangement arrangement, InitialExecutionState initialState, InstanceIDSource ids)
        {
            DirectiveInstance pragma = new DirectiveInstance(ids.Next(), StandardDirective());
            List<DirectiveInstance> pragmas = new List<DirectiveInstance> { pragma };
            CompiledSource source = new CompiledSource(arrangement, pragmas, StandardCost());

            return new ExecutionRequest(source, StandardConfiguration(), StandardStamps(), initialState);
        }

        private static InstructionDefinition AddValueInstruction(int constant)
        {
            return InstructionWith(new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(constant)));
        }

        private static InstructionDefinition ScoreAddValueInstruction()
        {
            return InstructionWith(new QuantityChangeOperation(CoreRegister.Score, QuantityOperator.Add, OperandSpec.FromRegister(CoreRegister.Value)));
        }

        private static InstructionDefinition InstructionWith(QuantityChangeOperation operation)
        {
            return new InstructionDefinition(
                new InstructionID("WB-INS-FIXTURE"),
                "rules",
                "instruction",
                ContentCategory.Instruction,
                Rarity.Common,
                Array.Empty<string>(),
                1,
                operation,
                null,
                Array.Empty<string>());
        }

        private static DirectiveDefinition StandardDirective()
        {
            return new DirectiveDefinition(
                new DirectiveID("WB-DIR-FIXTURE"),
                "rules",
                "directive",
                ContentCategory.Directive,
                Rarity.Common,
                Array.Empty<string>(),
                Array.Empty<EffectDefinition>());
        }

        private static CompilationCostBreakdown StandardCost()
        {
            return new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false);
        }
    }
}
