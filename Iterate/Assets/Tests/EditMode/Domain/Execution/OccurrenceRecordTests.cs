using System;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the typed occurrence records the effect engine matches against:
    /// <see cref="OperationOccurrence"/>'s operand-source pairing rule and
    /// <see cref="QuantityOccurrence"/>'s origin rules — a primary change carries its source
    /// ownership, a reaction-caused change carries its effect origin, never both ways.
    /// </summary>
    public sealed class OccurrenceRecordTests
    {
        [Test]
        public void OperationOccurrence_RegisterOperand_RoundTrips()
        {
            OperationOccurrence occurrence = new OperationOccurrence(
                new RuntimeUnitID(3),
                new TraceEventID(11),
                0,
                CoreRegister.Value,
                CoreLineOperator.Add,
                OperandSource.Register,
                CoreRegister.Signal,
                OwnershipClassification.PlayerOwned);

            Assert.AreEqual(new RuntimeUnitID(3), occurrence.Unit);
            Assert.AreEqual(new TraceEventID(11), occurrence.Event);
            Assert.AreEqual(0, occurrence.CausalDepth);
            Assert.AreEqual(CoreRegister.Value, occurrence.Register);
            Assert.AreEqual(CoreLineOperator.Add, occurrence.Operator);
            Assert.AreEqual(OperandSource.Register, occurrence.OperandSource);
            Assert.AreEqual(CoreRegister.Signal, occurrence.OperandRegister);
            Assert.AreEqual(OwnershipClassification.PlayerOwned, occurrence.Ownership);
        }

        [Test]
        public void OperationOccurrence_NegativeDepth_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OperationOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                -1,
                CoreRegister.Value,
                CoreLineOperator.Add,
                OperandSource.Constant,
                null,
                OwnershipClassification.PlayerOwned));
        }

        [Test]
        public void OperationOccurrence_RegisterOperandWithoutOperandRegister_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OperationOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                0,
                CoreRegister.Value,
                CoreLineOperator.Add,
                OperandSource.Register,
                null,
                OwnershipClassification.PlayerOwned));
        }

        [Test]
        public void OperationOccurrence_ConstantOperandWithOperandRegister_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OperationOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                0,
                CoreRegister.Value,
                CoreLineOperator.Add,
                OperandSource.Constant,
                CoreRegister.Signal,
                OwnershipClassification.PlayerOwned));
        }

        [Test]
        public void QuantityOccurrence_PrimaryChange_RoundTrips()
        {
            QuantityOccurrence occurrence = new QuantityOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(21),
                0,
                CoreRegister.Score,
                4,
                OwnershipClassification.PlayerOwned,
                null,
                true);

            Assert.AreEqual(new RuntimeUnitID(5), occurrence.Unit);
            Assert.AreEqual(new TraceEventID(21), occurrence.Event);
            Assert.AreEqual(0, occurrence.CausalDepth);
            Assert.AreEqual(CoreRegister.Score, occurrence.Register);
            Assert.AreEqual(4, occurrence.ActualDelta);
            Assert.AreEqual(OwnershipClassification.PlayerOwned, occurrence.Ownership);
            Assert.IsNull(occurrence.EffectOrigin);
            Assert.IsTrue(occurrence.FromPrimaryOperation);
        }

        [Test]
        public void QuantityOccurrence_ReactionChange_RoundTrips()
        {
            QuantityOccurrence occurrence = new QuantityOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(30),
                2,
                CoreRegister.Value,
                1,
                null,
                new InstanceID(9),
                false);

            Assert.AreEqual(2, occurrence.CausalDepth);
            Assert.IsNull(occurrence.Ownership);
            Assert.AreEqual(new InstanceID(9), occurrence.EffectOrigin);
            Assert.IsFalse(occurrence.FromPrimaryOperation);
        }

        [Test]
        public void QuantityOccurrence_NegativeDepth_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                -1,
                CoreRegister.Value,
                1,
                OwnershipClassification.PlayerOwned,
                null,
                true));
        }

        [Test]
        public void QuantityOccurrence_PrimaryWithoutOwnership_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                0,
                CoreRegister.Value,
                1,
                null,
                null,
                true));
        }

        [Test]
        public void QuantityOccurrence_PrimaryWithEffectOrigin_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                0,
                CoreRegister.Value,
                1,
                OwnershipClassification.PlayerOwned,
                new InstanceID(9),
                true));
        }

        [Test]
        public void QuantityOccurrence_ReactionWithoutEffectOrigin_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                1,
                CoreRegister.Value,
                1,
                null,
                null,
                false));
        }
    }
}
