using NUnit.Framework;
using Iterate.Domain.Content;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests that every <see cref="EffectOperation"/> derived record reports its correct
    /// <see cref="OperationKind"/>, that <see cref="OperandSpec"/> factories select the right source,
    /// and that operation records compare by value.
    /// </summary>
    public sealed class EffectOperationTests
    {
        [Test]
        public void QuantityChangeOperation_ReportsQuantityChangeKind()
        {
            EffectOperation operation = new QuantityChangeOperation(
                CoreRegister.Value,
                QuantityOperator.Add,
                OperandSpec.FromConstant(2)
            );

            Assert.AreEqual(OperationKind.QuantityChange, operation.Kind);
        }

        [Test]
        public void DispositionChangeOperation_ReportsDispositionChangeKind()
        {
            EffectOperation operation = new DispositionChangeOperation("SKIPPED");

            Assert.AreEqual(OperationKind.DispositionChange, operation.Kind);
        }

        [Test]
        public void AddedExecutionRequestOperation_ReportsAddedExecutionRequestKind()
        {
            EffectOperation operation = new AddedExecutionRequestOperation(
                new TargetingRule("MOST_RECENT_QUALIFYING_UNIT", string.Empty),
                true
            );

            Assert.AreEqual(OperationKind.AddedExecutionRequest, operation.Kind);
        }

        [Test]
        public void CounterRequestOperation_ReportsCounterRequestKind()
        {
            EffectOperation operation = new CounterRequestOperation("HEAT", 1, 0, 3, true, true);

            Assert.AreEqual(OperationKind.CounterRequest, operation.Kind);
        }

        [Test]
        public void CostModificationOperation_ReportsCostModificationKind()
        {
            EffectOperation operation = new CostModificationOperation("COMPILATION", false, -1, 0, false);

            Assert.AreEqual(OperationKind.CostModification, operation.Kind);
        }

        [Test]
        public void RescueOperation_ReportsRescueKind()
        {
            EffectOperation operation = new RescueOperation("RESCUED");

            Assert.AreEqual(OperationKind.Rescue, operation.Kind);
        }

        [Test]
        public void PredictionVisibilityOperation_ReportsPredictionVisibilityKind()
        {
            EffectOperation operation = new PredictionVisibilityOperation("CANONICAL_PREDICTION");

            Assert.AreEqual(OperationKind.PredictionVisibility, operation.Kind);
        }

        [Test]
        public void ConfigurationModificationOperation_ReportsConfigurationModificationKind()
        {
            EffectOperation operation = new ConfigurationModificationOperation("STARTING_BYTES", 1, false);

            Assert.AreEqual(OperationKind.ConfigurationModification, operation.Kind);
        }

        [Test]
        public void OperationModificationOperation_ReportsOperationModificationKind()
        {
            EffectOperation operation = new OperationModificationOperation(1);

            Assert.AreEqual(OperationKind.OperationModification, operation.Kind);
        }

        [Test]
        public void TargetLockUpdateOperation_ReportsTargetLockUpdateKind()
        {
            EffectOperation operation = new TargetLockUpdateOperation(
                new TargetingRule("MOST_RECENT_QUALIFYING_UNIT", string.Empty)
            );

            Assert.AreEqual(OperationKind.TargetLockUpdate, operation.Kind);
        }

        [Test]
        public void OperandSpec_FromConstant_CarriesConstantSource()
        {
            OperandSpec operand = OperandSpec.FromConstant(3);

            Assert.AreEqual(OperandSource.Constant, operand.Source);
            Assert.AreEqual(3, operand.Constant);
        }

        [Test]
        public void OperandSpec_FromRegister_CarriesRegisterSource()
        {
            OperandSpec operand = OperandSpec.FromRegister(CoreRegister.Signal);

            Assert.AreEqual(OperandSource.Register, operand.Source);
            Assert.AreEqual(CoreRegister.Signal, operand.SourceRegister);
        }

        [Test]
        public void OperandSpec_FromLineNumber_CarriesLineNumberSource()
        {
            OperandSpec operand = OperandSpec.FromLineNumber();

            Assert.AreEqual(OperandSource.LineNumber, operand.Source);
        }

        [Test]
        public void QuantityChangeOperation_SameValues_AreEqual()
        {
            QuantityChangeOperation first = new(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(2));
            QuantityChangeOperation second = new(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(2));

            Assert.AreEqual(first, second);
        }

        [Test]
        public void QuantityChangeOperation_DifferentOperand_AreNotEqual()
        {
            QuantityChangeOperation first = new(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(2));
            QuantityChangeOperation second = new(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(3));

            Assert.AreNotEqual(first, second);
        }
    }
}
