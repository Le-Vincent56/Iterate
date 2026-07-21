using System;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests that <see cref="ExecutionRegisters"/> seeds Value/Signal/Score from an
    /// <see cref="InitialExecutionState"/>, round-trips each register through <c>Read</c>/<c>Write</c>, and
    /// rejects a null initial state.
    /// </summary>
    public sealed class ExecutionRegistersTests
    {
        private static ExecutionRegisters Seeded()
        {
            return new ExecutionRegisters(new InitialExecutionState(new ValueAmount(5), new SignalValue(2), new ScoreValue(7)));
        }

        [Test]
        public void Constructor_SeedsFromInitialState()
        {
            ExecutionRegisters registers = Seeded();

            Assert.AreEqual(5, registers.Value);
            Assert.AreEqual(2, registers.Signal);
            Assert.AreEqual(7, registers.Score);
        }

        [Test]
        public void Read_ReturnsPerRegister()
        {
            ExecutionRegisters registers = Seeded();

            Assert.AreEqual(5, registers.Read(CoreRegister.Value));
            Assert.AreEqual(2, registers.Read(CoreRegister.Signal));
            Assert.AreEqual(7, registers.Read(CoreRegister.Score));
        }

        [Test]
        public void Write_ThenRead_RoundTripsValue()
        {
            ExecutionRegisters registers = Seeded();

            registers.Write(CoreRegister.Value, 42);

            Assert.AreEqual(42, registers.Read(CoreRegister.Value));
            Assert.AreEqual(42, registers.Value);
        }

        [Test]
        public void Write_ThenRead_RoundTripsSignal()
        {
            ExecutionRegisters registers = Seeded();

            registers.Write(CoreRegister.Signal, 9);

            Assert.AreEqual(9, registers.Read(CoreRegister.Signal));
            Assert.AreEqual(9, registers.Signal);
        }

        [Test]
        public void Write_ThenRead_RoundTripsScore()
        {
            ExecutionRegisters registers = Seeded();

            registers.Write(CoreRegister.Score, 13);

            Assert.AreEqual(13, registers.Read(CoreRegister.Score));
            Assert.AreEqual(13, registers.Score);
        }

        [Test]
        public void Constructor_NullInitialState_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionRegisters(null));
        }
    }
}
