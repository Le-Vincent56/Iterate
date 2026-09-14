using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the first evaluator of the three authored host-eligibility rules. The rule strings are a
    /// controlled vocabulary the catalog validates, so this evaluator and that vocabulary can only
    /// disagree if the coverage test below fails: every registered member must evaluate rather than
    /// throw, and an unregistered rule must throw rather than quietly answer false.
    /// </summary>
    public sealed class PatchHostRulesTests
    {
        private const string FixedNumberAddition = "FIXED_NUMBER_ADDITION_HOSTS";
        private const string OrdinaryInstruction = "ORDINARY_INSTRUCTION_HOSTS";
        private const string ScoreInstruction = "SCORE_INSTRUCTION_HOSTS";

        [Test]
        public void FixedNumberAddition_ValuePlusConstant_IsEligible()
        {
            Assert.IsTrue(PatchHostRules.IsEligible(
                PatchWith(FixedNumberAddition),
                InstructionHost(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1))));
        }

        [Test]
        public void FixedNumberAddition_SignalPlusConstant_IsEligible()
        {
            Assert.IsTrue(PatchHostRules.IsEligible(
                PatchWith(FixedNumberAddition),
                InstructionHost(CoreRegister.Signal, QuantityOperator.Add, OperandSpec.FromConstant(2))));
        }

        [Test]
        public void FixedNumberAddition_ScorePlusConstant_IsNotEligible()
        {
            Assert.IsFalse(PatchHostRules.IsEligible(
                PatchWith(FixedNumberAddition),
                InstructionHost(CoreRegister.Score, QuantityOperator.Add, OperandSpec.FromConstant(1))));
        }

        [Test]
        public void FixedNumberAddition_ValueTimesConstant_IsNotEligible()
        {
            Assert.IsFalse(PatchHostRules.IsEligible(
                PatchWith(FixedNumberAddition),
                InstructionHost(CoreRegister.Value, QuantityOperator.Multiply, OperandSpec.FromConstant(2))));
        }

        [Test]
        public void FixedNumberAddition_ValuePlusRegister_IsNotEligible()
        {
            Assert.IsFalse(PatchHostRules.IsEligible(
                PatchWith(FixedNumberAddition),
                InstructionHost(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromRegister(CoreRegister.Signal))));
        }

        [Test]
        public void FixedNumberAddition_ValuePlusLineNumber_IsNotEligible()
        {
            Assert.IsFalse(PatchHostRules.IsEligible(
                PatchWith(FixedNumberAddition),
                InstructionHost(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromLineNumber())));
        }

        [Test]
        public void OrdinaryInstruction_AnyInstruction_IsEligible()
        {
            Assert.IsTrue(PatchHostRules.IsEligible(
                PatchWith(OrdinaryInstruction),
                InstructionHost(CoreRegister.Value, QuantityOperator.Multiply, OperandSpec.FromConstant(2))));
            Assert.IsTrue(PatchHostRules.IsEligible(
                PatchWith(OrdinaryInstruction),
                InstructionHost(CoreRegister.Score, QuantityOperator.Add, OperandSpec.FromRegister(CoreRegister.Value))));
        }

        [Test]
        public void ScoreInstruction_ScoreHost_IsEligible()
        {
            Assert.IsTrue(PatchHostRules.IsEligible(
                PatchWith(ScoreInstruction),
                InstructionHost(CoreRegister.Score, QuantityOperator.Add, OperandSpec.FromRegister(CoreRegister.Value))));
        }

        [Test]
        public void ScoreInstruction_ValueHost_IsNotEligible()
        {
            Assert.IsFalse(PatchHostRules.IsEligible(
                PatchWith(ScoreInstruction),
                InstructionHost(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1))));
        }

        [Test]
        public void IneligibilityTags_NamingThePatch_BlockAnOtherwiseEligibleHost()
        {
            PatchDefinition patch = PatchWith(FixedNumberAddition, "CONSTANT PATCH");
            RepositoryEntry host = InstructionHost(
                CoreRegister.Value,
                QuantityOperator.Add,
                OperandSpec.FromConstant(1),
                new[] { "STANDARD LIBRARY", "CONSTANT PATCH" });

            Assert.IsFalse(PatchHostRules.IsEligible(patch, host));
        }

        [Test]
        public void IneligibilityTags_NamingAnotherModifier_DoNotBlock()
        {
            PatchDefinition patch = PatchWith(FixedNumberAddition, "CONSTANT PATCH");
            RepositoryEntry host = InstructionHost(
                CoreRegister.Value,
                QuantityOperator.Add,
                OperandSpec.FromConstant(1),
                new[] { "STANDARD LIBRARY" });

            Assert.IsTrue(PatchHostRules.IsEligible(patch, host));
        }

        [Test]
        public void Structures_AreNeverEligible()
        {
            Assert.IsFalse(PatchHostRules.IsEligible(PatchWith(OrdinaryInstruction), StructureHost()));
        }

        [Test]
        public void Directives_AreNeverEligible()
        {
            Assert.IsFalse(PatchHostRules.IsEligible(PatchWith(OrdinaryInstruction), DirectiveHost()));
        }

        [Test]
        public void AnUnregisteredRule_Throws()
        {
            Assert.Throws<ArgumentException>(() => PatchHostRules.IsEligible(
                PatchWith("NO_SUCH_RULE"),
                InstructionHost(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1))));
        }

        [Test]
        public void EveryVocabularyMember_IsEvaluatedRatherThanThrown()
        {
            RepositoryEntry host = InstructionHost(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1));

            Assert.AreEqual(3, CatalogVocabulary.PatchHostEligibilityRules.Count);
            foreach (string member in CatalogVocabulary.PatchHostEligibilityRules)
            {
                string rule = member;
                Assert.DoesNotThrow(
                    () => PatchHostRules.IsEligible(PatchWith(rule), host),
                    rule + " is registered but not evaluated");
            }
        }

        private static PatchDefinition PatchWith(string rule, string displayName = "TEST PATCH")
        {
            return new PatchDefinition(
                new PatchID("WB-PAT-001"),
                "Test rules.",
                displayName,
                ContentCategory.Patch,
                Rarity.Common,
                Array.Empty<string>(),
                new PatchHostEligibility(rule),
                Array.Empty<EffectDefinition>());
        }

        private static RepositoryEntry InstructionHost(
            CoreRegister register,
            QuantityOperator quantityOperator,
            OperandSpec operand,
            IReadOnlyList<string> ineligibilityTags = null)
        {
            InstructionDefinition definition = new(
                new InstructionID("WB-INS-101"),
                "Test rules.",
                "TEST INSTRUCTION",
                ContentCategory.Instruction,
                Rarity.Common,
                Array.Empty<string>(),
                1,
                new QuantityChangeOperation(register, quantityOperator, operand),
                null,
                ineligibilityTags ?? Array.Empty<string>());

            return new RepositoryEntry(
                RepositoryItem.From(definition, new InstanceID(10)),
                AcquisitionOrigin.Purchase,
                false,
                1);
        }

        private static RepositoryEntry StructureHost()
        {
            return new RepositoryEntry(
                RepositoryItem.From(ProgressionFixtures.Structure(ProgressionFixtures.RepeatTwo), new InstanceID(20)),
                AcquisitionOrigin.Purchase,
                false,
                1);
        }

        private static RepositoryEntry DirectiveHost()
        {
            return new RepositoryEntry(
                RepositoryItem.From(ProgressionFixtures.Directive(ProgressionFixtures.Overclock), new InstanceID(30)),
                AcquisitionOrigin.Purchase,
                false,
                1);
        }
    }
}
