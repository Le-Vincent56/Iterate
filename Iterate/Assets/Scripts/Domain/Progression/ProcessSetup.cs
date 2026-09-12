namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A Process's resolved setup: the authored baseline after every process-setup effect has folded
    /// over it. Immutable, and resolved before Branch configuration, because a Utility may widen the
    /// Branch the player is about to fill.
    /// </summary>
    /// <param name="BufferCapacity">The Instruction Buffer's slot count.</param>
    /// <param name="StartingBytes">The Bytes the Process opens with.</param>
    /// <param name="BranchCapacity">The Active Branch capacity this Process allows.</param>
    /// <param name="Executions">The execution allowance.</param>
    /// <param name="MandatoryExecutions">Whether every allowed execution must be run.</param>
    public readonly record struct ProcessSetup(
        int BufferCapacity,
        int StartingBytes,
        int BranchCapacity,
        int Executions,
        bool MandatoryExecutions
    );
}