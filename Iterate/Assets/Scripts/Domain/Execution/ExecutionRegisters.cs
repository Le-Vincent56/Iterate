using System;
using Iterate.Domain.Content;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The three mutable Core registers for one execution, seeded from an
    /// <see cref="InitialExecutionState"/>. The scheduler owns all mutation: evaluation reads these
    /// registers but never writes them, and only the scheduler calls <see cref="Write"/>.
    /// </summary>
    public sealed class ExecutionRegisters
    {
        /// <summary>
        /// The current Value register.
        /// </summary>
        public int Value { get; private set; }

        /// <summary>
        /// The current Signal register.
        /// </summary>
        public int Signal { get; private set; }

        /// <summary>
        /// The current Score register.
        /// </summary>
        public int Score { get; private set; }

        public ExecutionRegisters(InitialExecutionState initialState)
        {
            if (initialState == null)
                throw new ArgumentException("ExecutionRegisters require an initial state.", nameof(initialState));

            Value = initialState.InitialValue.Value;
            Signal = initialState.InitialSignal.Value;
            Score = initialState.InitialScore.Value;
        }

        /// <summary>
        /// Reads the current value of a register.
        /// </summary>
        /// <param name="register">The register to read.</param>
        /// <returns>The register's current value.</returns>
        /// <exception cref="ArgumentException">Thrown when the register is not a known member.</exception>
        public int Read(CoreRegister register)
        {
            switch (register)
            {
                case CoreRegister.Value:
                    return Value;
                
                case CoreRegister.Signal:
                    return Signal;
                
                case CoreRegister.Score:
                    return Score;
                
                default:
                    throw new ArgumentException($"Unknown register {register}.", nameof(register));
            }
        }

        /// <summary>
        /// Writes a value to a register.
        /// </summary>
        /// <param name="register">The register to write.</param>
        /// <param name="value">The value to store.</param>
        /// <exception cref="ArgumentException">Thrown when the register is not a known member.</exception>
        public void Write(CoreRegister register, int value)
        {
            switch (register)
            {
                case CoreRegister.Value:
                    Value = value;
                    break;
                
                case CoreRegister.Signal:
                    Signal = value;
                    break;
                
                case CoreRegister.Score:
                    Score = value;
                    break;
                
                default:
                    throw new ArgumentException($"Unknown register {register}.", nameof(register));
            }
        }
    }
}