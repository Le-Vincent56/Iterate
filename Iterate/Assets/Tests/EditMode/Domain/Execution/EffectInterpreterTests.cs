using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the closed, fail-fast <see cref="EffectInterpreter"/>: interpretable EXECUTION effects
    /// become <see cref="ActiveEffect"/>s with the modification/reaction/rescue kind keyed to the
    /// trigger pair and its band, non-EXECUTION domains are skipped, and every token outside the
    /// closed vocabulary throws naming the offender.
    /// </summary>
    public sealed class EffectInterpreterTests
    {
        [Test]
        public void Interpret_ParallelChannelShape_YieldsReaction()
        {
            DependencyInstance dependency = Instance(7, Dependency("WB-DEP-904", ParallelChannelEffect()));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual(1, effects.Count);
            ActiveEffect effect = effects[0];
            Assert.AreEqual(new InstanceID(7), effect.Origin);
            Assert.AreEqual("WB-DEP-904", effect.DefinitionID);
            Assert.AreEqual(0, effect.EffectIndex);
            Assert.IsFalse(effect.IsModification);
            Assert.AreEqual("QUANTITY_CHANGED", effect.Trigger.EventSubtype);
            Assert.AreEqual(CoreRegister.Value, effect.Operation.Register);
            Assert.AreEqual(QuantityOperator.Add, effect.Operation.Operator);
            Assert.AreEqual(1, effect.Operation.Operand.Constant);
            Assert.AreEqual("EVERY_QUALIFYING_EVENT", effect.Frequency.Allowance);
        }

        [Test]
        public void Interpret_StandardLibraryShape_YieldsModification()
        {
            DependencyInstance dependency = Instance(3, Dependency("WB-DEP-901", StandardLibraryEffect()));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual(1, effects.Count);
            Assert.IsTrue(effects[0].IsModification);
            Assert.AreEqual("PRIMARY_OPERATION_PENDING", effects[0].Trigger.EventSubtype);
        }

        [Test]
        public void Interpret_FrequencyKey_ComposesDefinitionIndexInstance()
        {
            DependencyInstance dependency = Instance(7, Dependency("WB-DEP-904", ParallelChannelEffect()));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual("WB-DEP-904:0#7", effects[0].FrequencyKey);
        }

        [Test]
        public void Interpret_TwoEffects_YieldIndicesZeroAndOne()
        {
            DependencyInstance dependency = Instance(2, Dependency("WB-DEP-905", ParallelChannelEffect(), StandardLibraryEffect()));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual(2, effects.Count);
            Assert.AreEqual(0, effects[0].EffectIndex);
            Assert.AreEqual(1, effects[1].EffectIndex);
            Assert.AreEqual("WB-DEP-905:1#2", effects[1].FrequencyKey);
        }

        [Test]
        public void Interpret_CompilationDomain_IsSkipped()
        {
            DependencyInstance dependency = Instance(4, Dependency("WB-DEP-902", CleanBuildEffect()));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual(0, effects.Count);
        }

        [Test]
        public void Interpret_EmptyEffects_YieldsEmpty()
        {
            DependencyInstance dependency = Instance(5, Dependency("WB-DEP-903"));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual(0, effects.Count);
        }

        [Test]
        public void Interpret_RescueOperation_ThrowsNamingKind()
        {
            EffectDefinition rescue = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity, Qualifier("REGISTER", "SCORE")),
                new RescueOperation("RESCUED"),
                Frequency("FIRST_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-907", rescue));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("Rescue", exception.Message);
        }

        [Test]
        public void Interpret_UnknownSubtype_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_RESET", EventFamily.Quantity),
                Operation(),
                Frequency("EVERY_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-910", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("QUANTITY_RESET", exception.Message);
        }

        [Test]
        public void Interpret_ReactionBandOnPendingSubtype_Throws()
        {
            EffectDefinition effect = Effect(
                Trigger(EventFamily.Operation, "PRIMARY_OPERATION_PENDING", "IMMEDIATE_RESULT_REACTION"),
                Operation(),
                Frequency("FIRST_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-911", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("IMMEDIATE_RESULT_REACTION", exception.Message);
        }

        [Test]
        public void Interpret_ModificationBandOnQuantityChanged_Throws()
        {
            EffectDefinition effect = Effect(
                Trigger(EventFamily.Quantity, "QUANTITY_CHANGED", "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION"),
                Operation(),
                Frequency("EVERY_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-912", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION", exception.Message);
        }

        [Test]
        public void Interpret_UnknownQualifierKind_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity, Qualifier("HOST_TAG", "VALUE")),
                Operation(),
                Frequency("EVERY_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-913", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("HOST_TAG", exception.Message);
        }

        [Test]
        public void Interpret_UnknownQualifierValue_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity, Qualifier("ACTUAL_DELTA_SIGN", "NEGATIVE")),
                Operation(),
                Frequency("EVERY_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-914", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("NEGATIVE", exception.Message);
        }

        [Test]
        public void Interpret_UnknownAllowance_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity),
                Operation(),
                Frequency("EVERY_QUALIFYING_PROCESS"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-915", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("EVERY_QUALIFYING_PROCESS", exception.Message);
        }

        [Test]
        public void Interpret_NullTrigger_Throws()
        {
            EffectDefinition effect = Effect(null, Operation(), Frequency("EVERY_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-916", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("trigger", exception.Message);
        }

        [Test]
        public void Interpret_RegisterOperandReaction_Throws()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity),
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromRegister(CoreRegister.Signal)),
                Frequency("EVERY_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-917", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("Register", exception.Message);
        }

        [Test]
        public void Interpret_SafeModeShape_YieldsRescue()
        {
            DependencyInstance dependency = Instance(9, Dependency("WB-DEP-907", SafeModeEffect()));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual(1, effects.Count);
            ActiveEffect effect = effects[0];
            Assert.AreEqual(ActiveEffectKind.Rescue, effect.Kind);
            Assert.IsNotNull(effect.Rescue);
            Assert.AreEqual("RESCUED", effect.Rescue.ResultingDisposition);
            Assert.IsNull(effect.Operation);
            Assert.IsFalse(effect.IsModification);
            Assert.AreEqual("SOURCE_EXECUTION_SKIPPED", effect.Trigger.EventSubtype);
            Assert.AreEqual("FIRST_QUALIFYING_EVENT", effect.Frequency.Allowance);
        }

        [Test]
        public void Interpret_RescueResultingSkipped_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                RescueTrigger(),
                new RescueOperation("SKIPPED"),
                Frequency("FIRST_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-940", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("SKIPPED", exception.Message);
        }

        [Test]
        public void Interpret_QualifierOnRescuePair_ThrowsNamingToken()
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Disposition,
                "SOURCE_EXECUTION_SKIPPED",
                new List<TriggerQualifier> { Qualifier("REGISTER", "VALUE") },
                new EffectTiming(TimingKind.Band, "QUALIFICATION_AND_PRE_OPERATION_INTERVENTION"));
            EffectDefinition effect = Effect(trigger, new RescueOperation("RESCUED"), Frequency("FIRST_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-941", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("REGISTER", exception.Message);
        }

        [Test]
        public void Interpret_RescuePairOnReactionBand_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                Trigger(EventFamily.Disposition, "SOURCE_EXECUTION_SKIPPED", "IMMEDIATE_RESULT_REACTION"),
                new RescueOperation("RESCUED"),
                Frequency("FIRST_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-942", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("IMMEDIATE_RESULT_REACTION", exception.Message);
        }

        [Test]
        public void Interpret_RescueOperationOnPendingTrigger_ThrowsNamingKind()
        {
            EffectDefinition effect = Effect(
                Trigger(EventFamily.Operation, "PRIMARY_OPERATION_PENDING", "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION"),
                new RescueOperation("RESCUED"),
                Frequency("FIRST_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-943", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("Rescue", exception.Message);
        }

        [Test]
        public void Interpret_QuantityChangeOnRescuePair_ThrowsNamingKind()
        {
            EffectDefinition effect = Effect(
                RescueTrigger(),
                Operation(),
                Frequency("FIRST_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-944", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("QuantityChange", exception.Message);
        }

        [Test]
        public void Interpret_QuantityChangeOnPostUnitPair_ThrowsNamingKind()
        {
            EffectDefinition effect = Effect(
                Trigger(EventFamily.Lifecycle, "RUNTIME_UNIT_COMPLETED", "POST_UNIT_CONSEQUENCE_AND_EVIDENCE"),
                Operation(),
                Frequency("EVERY_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-945", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("QuantityChange", exception.Message);
        }

        [Test]
        public void Interpret_ConditionTrueOnReactionBand_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                Trigger(EventFamily.Structure, "CONDITION_TRUE", "IMMEDIATE_RESULT_REACTION"),
                Operation(),
                Frequency("EVERY_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-946", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("CONDITION_TRUE", exception.Message);
        }

        [Test]
        public void Interpret_QualifierFreeAddedExecutionRequest_YieldsAddedExecution()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity),
                new AddedExecutionRequestOperation(new TargetingRule("TRIGGERING_UNIT", string.Empty), false),
                Frequency("EVERY_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-947", effect));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual(1, effects.Count);
            Assert.AreEqual(ActiveEffectKind.AddedExecution, effects[0].Kind);
            Assert.AreEqual("TRIGGERING_UNIT", effects[0].Request.Target.Kind);
            Assert.IsNull(effects[0].Operation);
        }

        [Test]
        public void Interpret_LineNumberOperandModification_Throws()
        {
            EffectDefinition effect = Effect(
                Trigger(EventFamily.Operation, "PRIMARY_OPERATION_PENDING", "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION"),
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromLineNumber()),
                Frequency("FIRST_QUALIFYING_EVENT"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-918", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("LineNumber", exception.Message);
        }

        [Test]
        public void Interpret_OverclockShape_YieldsAddedExecution()
        {
            DependencyInstance dependency = Instance(6, Dependency("WB-DIR-901", OverclockEffect(false)));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual(1, effects.Count);
            ActiveEffect effect = effects[0];
            Assert.AreEqual(ActiveEffectKind.AddedExecution, effect.Kind);
            Assert.AreEqual("TRIGGERING_UNIT", effect.Request.Target.Kind);
            Assert.IsFalse(effect.Request.CancelOnInvalid);
            Assert.IsNull(effect.Operation);
            Assert.IsNull(effect.BoundaryName);
            Assert.AreEqual("ONCE", effect.Frequency.Allowance);
        }

        [Test]
        public void Interpret_PreFixOverclockShape_StillInterprets()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger(
                    "QUANTITY_CHANGED",
                    EventFamily.Quantity,
                    Qualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                    Qualifier("REGISTER", "VALUE")),
                new AddedExecutionRequestOperation(new TargetingRule("TRIGGERING_UNIT", string.Empty), false),
                new EffectFrequency("ONCE", "EXECUTION"));
            DependencyInstance dependency = Instance(6, Dependency("WB-DIR-902", effect));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual(1, effects.Count);
            Assert.AreEqual(ActiveEffectKind.AddedExecution, effects[0].Kind);
        }

        [Test]
        public void Interpret_LoopUnrollerShape_YieldsAddedExecution()
        {
            DependencyInstance dependency = Instance(7, Dependency("WB-DEP-950", LoopUnrollerEffect()));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual(1, effects.Count);
            ActiveEffect effect = effects[0];
            Assert.AreEqual(ActiveEffectKind.AddedExecution, effect.Kind);
            Assert.AreEqual("RUNTIME_UNIT_COMPLETED", effect.Trigger.EventSubtype);
            Assert.AreEqual("TRIGGERING_UNIT", effect.Request.Target.Kind);
            Assert.AreEqual("FIRST_QUALIFYING_EVENT", effect.Frequency.Allowance);
        }

        [Test]
        public void Interpret_BranchPredictorShape_YieldsAddedExecution()
        {
            DependencyInstance dependency = Instance(8, Dependency("WB-DEP-951", BranchPredictorEffect()));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual(1, effects.Count);
            ActiveEffect effect = effects[0];
            Assert.AreEqual(ActiveEffectKind.AddedExecution, effect.Kind);
            Assert.AreEqual("CONDITION_TRUE", effect.Trigger.EventSubtype);
            Assert.AreEqual("FIRST_CONTAINED_INSTRUCTION", effect.Request.Target.Kind);
        }

        [Test]
        public void Interpret_AlignShape_YieldsBoundary()
        {
            DependencyInstance dependency = Instance(9, Dependency("WB-DIR-903", AlignEffect()));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual(1, effects.Count);
            ActiveEffect effect = effects[0];
            Assert.AreEqual(ActiveEffectKind.Boundary, effect.Kind);
            Assert.AreEqual("END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL", effect.BoundaryName);
            Assert.IsNotNull(effect.Operation);
            Assert.AreEqual(CoreRegister.Value, effect.Operation.Register);
            Assert.IsNull(effect.Request);
            Assert.AreEqual("ONCE", effect.Frequency.Allowance);
        }

        [Test]
        public void Interpret_PreFixAlignFamily_ThrowsNamingPair()
        {
            EffectDefinition effect = Effect(
                new TriggerDescriptor(
                    EventFamily.Lifecycle,
                    "BOUNDARY_EFFECT_REQUESTED",
                    new List<TriggerQualifier> { Qualifier("PARITY", "ODD"), Qualifier("REGISTER", "VALUE") },
                    new EffectTiming(TimingKind.NamedBoundary, "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL")),
                Operation(),
                new EffectFrequency("ONCE", "EXECUTION"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DIR-904", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("BOUNDARY_EFFECT_REQUESTED", exception.Message);
            StringAssert.Contains("Lifecycle", exception.Message);
        }

        [Test]
        public void Interpret_Directive_YieldsDirectiveOrigin()
        {
            DirectiveInstance directive = new DirectiveInstance(new InstanceID(21), Directive("WB-DIR-905", OverclockEffect(false)));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(directive);

            Assert.AreEqual(1, effects.Count);
            Assert.AreEqual(new InstanceID(21), effects[0].Origin);
            Assert.AreEqual("WB-DIR-905", effects[0].DefinitionID);
            Assert.AreEqual(ActiveEffectKind.AddedExecution, effects[0].Kind);
            Assert.AreEqual("WB-DIR-905:0#21", effects[0].FrequencyKey);
        }

        [Test]
        public void Interpret_NullDirective_Throws()
        {
            Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret((DirectiveInstance)null));
        }

        [Test]
        public void Interpret_OnceAllowanceOnReaction_Accepted()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity),
                Operation(),
                new EffectFrequency("ONCE", "EXECUTION"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-952", effect));

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(dependency);

            Assert.AreEqual("ONCE", effects[0].Frequency.Allowance);
        }

        [Test]
        public void Interpret_UpToNAllowance_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity),
                Operation(),
                new EffectFrequency("UP_TO_N", "EXECUTION"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-953", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("UP_TO_N", exception.Message);
        }

        [Test]
        public void Interpret_CancelOnInvalidTrue_Throws()
        {
            DependencyInstance dependency = Instance(1, Dependency("WB-DIR-906", OverclockEffect(true)));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("CancelOnInvalid", exception.Message);
        }

        [Test]
        public void Interpret_LockedTargetTargeting_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity),
                new AddedExecutionRequestOperation(new TargetingRule("LOCKED_TARGET", string.Empty), false),
                new EffectFrequency("ONCE", "EXECUTION"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DIR-907", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("LOCKED_TARGET", exception.Message);
        }

        [Test]
        public void Interpret_MostRecentQualifyingUnitTargeting_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity),
                new AddedExecutionRequestOperation(new TargetingRule("MOST_RECENT_QUALIFYING_UNIT", string.Empty), false),
                new EffectFrequency("ONCE", "EXECUTION"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DIR-908", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("MOST_RECENT_QUALIFYING_UNIT", exception.Message);
        }

        [Test]
        public void Interpret_FirstContainedInstructionOnQuantityPair_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity),
                new AddedExecutionRequestOperation(new TargetingRule("FIRST_CONTAINED_INSTRUCTION", string.Empty), false),
                new EffectFrequency("ONCE", "EXECUTION"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DIR-909", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("FIRST_CONTAINED_INSTRUCTION", exception.Message);
        }

        [Test]
        public void Interpret_TargetLockUpdateOperation_ThrowsNamingKind()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity),
                new TargetLockUpdateOperation(new TargetingRule("MOST_RECENT_QUALIFYING_UNIT", string.Empty)),
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DIR-910", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("TargetLockUpdate", exception.Message);
        }

        [Test]
        public void Interpret_UnwiredBoundaryName_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                new TriggerDescriptor(
                    EventFamily.Reaction,
                    "BOUNDARY_EFFECT_REQUESTED",
                    new List<TriggerQualifier> { Qualifier("PARITY", "ODD"), Qualifier("REGISTER", "VALUE") },
                    new EffectTiming(TimingKind.NamedBoundary, "BEFORE_DESIGNATED_CORE_OUTPUT")),
                Operation(),
                new EffectFrequency("ONCE", "EXECUTION"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DIR-911", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("BEFORE_DESIGNATED_CORE_OUTPUT", exception.Message);
        }

        [Test]
        public void Interpret_StructureContextOnQuantityPair_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                ReactionTrigger("QUANTITY_CHANGED", EventFamily.Quantity, Qualifier("STRUCTURE_CONTEXT", "INSIDE_REPEAT")),
                new AddedExecutionRequestOperation(new TargetingRule("TRIGGERING_UNIT", string.Empty), false),
                new EffectFrequency("ONCE", "EXECUTION"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-954", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("STRUCTURE_CONTEXT", exception.Message);
        }

        [Test]
        public void Interpret_ParityWithoutRegister_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                new TriggerDescriptor(
                    EventFamily.Reaction,
                    "BOUNDARY_EFFECT_REQUESTED",
                    new List<TriggerQualifier> { Qualifier("PARITY", "ODD") },
                    new EffectTiming(TimingKind.NamedBoundary, "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL")),
                Operation(),
                new EffectFrequency("ONCE", "EXECUTION"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DIR-912", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("PARITY", exception.Message);
        }

        [Test]
        public void Interpret_QualifierOnConditionTruePair_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                new TriggerDescriptor(
                    EventFamily.Structure,
                    "CONDITION_TRUE",
                    new List<TriggerQualifier> { Qualifier("REGISTER", "VALUE") },
                    new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE")),
                new AddedExecutionRequestOperation(new TargetingRule("FIRST_CONTAINED_INSTRUCTION", string.Empty), false),
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-955", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("REGISTER", exception.Message);
        }

        [Test]
        public void Interpret_TriggeringUnitOnConditionTruePair_ThrowsNamingToken()
        {
            EffectDefinition effect = Effect(
                new TriggerDescriptor(
                    EventFamily.Structure,
                    "CONDITION_TRUE",
                    new List<TriggerQualifier>(),
                    new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE")),
                new AddedExecutionRequestOperation(new TargetingRule("TRIGGERING_UNIT", string.Empty), false),
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION"));
            DependencyInstance dependency = Instance(1, Dependency("WB-DEP-956", effect));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => EffectInterpreter.Interpret(dependency));

            StringAssert.Contains("TRIGGERING_UNIT", exception.Message);
        }

        /// <summary>
        /// Wraps a definition in a Dependency instance with the given identity.
        /// </summary>
        /// <param name="instanceID">The instance identity value.</param>
        /// <param name="definition">The frozen definition.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance Instance(int instanceID, DependencyDefinition definition)
        {
            return new DependencyInstance(new InstanceID(instanceID), definition);
        }

        /// <summary>
        /// Builds a frozen Dependency definition carrying the given effects.
        /// </summary>
        /// <param name="id">The definition's surrogate-key identity.</param>
        /// <param name="effects">The declarative effects.</param>
        /// <returns>The frozen definition.</returns>
        private static DependencyDefinition Dependency(string id, params EffectDefinition[] effects)
        {
            return new DependencyDefinition(
                new DependencyID(id),
                "Test rules.",
                "TEST DEPENDENCY",
                ContentCategory.Dependency,
                Rarity.Starter,
                new List<string>(),
                0,
                effects);
        }

        /// <summary>
        /// The PARALLEL-CHANNEL-shaped effect: every positive Signal delta adds 1 to Value at the
        /// immediate-reaction band.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition ParallelChannelEffect()
        {
            return Effect(
                ReactionTrigger(
                    "QUANTITY_CHANGED",
                    EventFamily.Quantity,
                    Qualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                    Qualifier("REGISTER", "SIGNAL")),
                Operation(),
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
        }

        /// <summary>
        /// The STANDARD-LIBRARY-shaped effect: the first player fixed addition to Value gains +1 at the
        /// modification band.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition StandardLibraryEffect()
        {
            return Effect(
                new TriggerDescriptor(
                    EventFamily.Operation,
                    "PRIMARY_OPERATION_PENDING",
                    new List<TriggerQualifier>
                    {
                        Qualifier("OPERATION_CLASS", "FIXED_ADDITION"),
                        Qualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION"),
                        Qualifier("REGISTER", "VALUE")
                    },
                    new EffectTiming(TimingKind.Band, "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION")),
                Operation(),
                Frequency("FIRST_QUALIFYING_EVENT"));
        }

        /// <summary>
        /// The SAFE-MODE-shaped rescue effect: the skipped-execution disposition trigger at the
        /// pre-operation band, no qualifiers, resolving to RESCUED once per execution.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition SafeModeEffect()
        {
            return Effect(
                RescueTrigger(),
                new RescueOperation("RESCUED"),
                Frequency("FIRST_QUALIFYING_EVENT"));
        }

        /// <summary>
        /// The qualifier-free rescue trigger pair at the pre-operation band.
        /// </summary>
        /// <returns>The trigger descriptor.</returns>
        private static TriggerDescriptor RescueTrigger()
        {
            return Trigger(EventFamily.Disposition, "SOURCE_EXECUTION_SKIPPED", "QUALIFICATION_AND_PRE_OPERATION_INTERVENTION");
        }

        /// <summary>
        /// The CLEAN-BUILD-shaped COMPILATION-domain effect the interpreter must skip.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition CleanBuildEffect()
        {
            return new EffectDefinition(
                PhaseDomain.Compilation,
                new TriggerDescriptor(
                    EventFamily.Lifecycle,
                    "COMPILATION_COMMITTED",
                    new List<TriggerQualifier> { Qualifier("OPERATION_CLASS", "EDITED_COMPILATION") },
                    null),
                new CostModificationOperation("COMPILATION", true, 0, 0, true),
                new TargetingRule("NO_TARGET", string.Empty),
                null,
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "PROCESS"));
        }

        /// <summary>
        /// Builds an EXECUTION-domain effect from a trigger, operation, and frequency.
        /// </summary>
        /// <param name="trigger">The trigger descriptor, or null.</param>
        /// <param name="operation">The effect operation.</param>
        /// <param name="frequency">The effect frequency.</param>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition Effect(
            TriggerDescriptor trigger,
            EffectOperation operation,
            EffectFrequency frequency)
        {
            return new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                operation,
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                trigger?.Timing,
                StackingMode.IndependentResolution,
                frequency);
        }

        /// <summary>
        /// Builds a reaction-band trigger for the given subtype and family.
        /// </summary>
        /// <param name="subtype">The event-subtype token.</param>
        /// <param name="family">The event family.</param>
        /// <param name="qualifiers">The trigger qualifiers.</param>
        /// <returns>The trigger descriptor.</returns>
        private static TriggerDescriptor ReactionTrigger(
            string subtype,
            EventFamily family,
            params TriggerQualifier[] qualifiers)
        {
            return new TriggerDescriptor(
                family,
                subtype,
                qualifiers,
                new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION"));
        }

        /// <summary>
        /// Builds a qualifier-free trigger with an explicit band name.
        /// </summary>
        /// <param name="family">The event family.</param>
        /// <param name="subtype">The event-subtype token.</param>
        /// <param name="band">The timing-band token.</param>
        /// <returns>The trigger descriptor.</returns>
        private static TriggerDescriptor Trigger(
            EventFamily family,
            string subtype,
            string band)
        {
            return new TriggerDescriptor(
                family,
                subtype,
                new List<TriggerQualifier>(),
                new EffectTiming(TimingKind.Band, band));
        }

        /// <summary>
        /// Builds one trigger qualifier.
        /// </summary>
        /// <param name="kind">The qualifier kind token.</param>
        /// <param name="value">The qualifier value token.</param>
        /// <returns>The qualifier.</returns>
        private static TriggerQualifier Qualifier(string kind, string value)
        {
            return new TriggerQualifier(kind, value);
        }

        /// <summary>
        /// The standard constant-operand quantity-change reaction operation.
        /// </summary>
        /// <returns>The operation.</returns>
        private static QuantityChangeOperation Operation()
        {
            return new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1));
        }

        /// <summary>
        /// Builds an EXECUTION-scoped frequency with the given allowance.
        /// </summary>
        /// <param name="allowance">The allowance token.</param>
        /// <returns>The frequency.</returns>
        private static EffectFrequency Frequency(string allowance)
        {
            return new EffectFrequency(allowance, "EXECUTION");
        }

        /// <summary>
        /// Builds a frozen Directive definition carrying the given effects.
        /// </summary>
        /// <param name="id">The definition's surrogate-key identity.</param>
        /// <param name="effects">The declarative effects.</param>
        /// <returns>The frozen definition.</returns>
        private static DirectiveDefinition Directive(string id, params EffectDefinition[] effects)
        {
            return new DirectiveDefinition(
                new DirectiveID(id),
                "Test rules.",
                "TEST DIRECTIVE",
                ContentCategory.Directive,
                Rarity.Uncommon,
                new List<string>(),
                effects);
        }

        /// <summary>
        /// The post-fix OVERCLOCK-shaped effect: the first positive player Value gain requests one
        /// added execution of the triggering unit, once per execution.
        /// </summary>
        /// <param name="cancelOnInvalid">The operation's cancel-on-invalid flag.</param>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition OverclockEffect(bool cancelOnInvalid)
        {
            return Effect(
                ReactionTrigger(
                    "QUANTITY_CHANGED",
                    EventFamily.Quantity,
                    Qualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                    Qualifier("REGISTER", "VALUE"),
                    Qualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION")),
                new AddedExecutionRequestOperation(new TargetingRule("TRIGGERING_UNIT", string.Empty), cancelOnInvalid),
                new EffectFrequency("ONCE", "EXECUTION"));
        }

        /// <summary>
        /// The LOOP-UNROLLER-shaped effect: the first Repeat-context runtime-unit completion requests
        /// one added execution of the triggering unit.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition LoopUnrollerEffect()
        {
            return Effect(
                new TriggerDescriptor(
                    EventFamily.Lifecycle,
                    "RUNTIME_UNIT_COMPLETED",
                    new List<TriggerQualifier> { Qualifier("STRUCTURE_CONTEXT", "INSIDE_REPEAT") },
                    new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE")),
                new AddedExecutionRequestOperation(new TargetingRule("TRIGGERING_UNIT", string.Empty), false),
                Frequency("FIRST_QUALIFYING_EVENT"));
        }

        /// <summary>
        /// The BRANCH-PREDICTOR-shaped effect: the first successful Condition evaluation requests one
        /// added execution of its first contained Instruction.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition BranchPredictorEffect()
        {
            return Effect(
                new TriggerDescriptor(
                    EventFamily.Structure,
                    "CONDITION_TRUE",
                    new List<TriggerQualifier>(),
                    new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE")),
                new AddedExecutionRequestOperation(new TargetingRule("FIRST_CONTAINED_INSTRUCTION", string.Empty), false),
                Frequency("FIRST_QUALIFYING_EVENT"));
        }

        /// <summary>
        /// The post-fix ALIGN-shaped effect: at the end-of-player-traversal boundary, if Value is odd,
        /// Value increases by 1, once per execution.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition AlignEffect()
        {
            return Effect(
                new TriggerDescriptor(
                    EventFamily.Reaction,
                    "BOUNDARY_EFFECT_REQUESTED",
                    new List<TriggerQualifier> { Qualifier("PARITY", "ODD"), Qualifier("REGISTER", "VALUE") },
                    new EffectTiming(TimingKind.NamedBoundary, "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL")),
                Operation(),
                new EffectFrequency("ONCE", "EXECUTION"));
        }
    }
}
