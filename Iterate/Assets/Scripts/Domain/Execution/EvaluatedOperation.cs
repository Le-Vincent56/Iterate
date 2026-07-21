using Iterate.Domain.Content;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The pure result of evaluating one operation against the current registers: the target register, the
    /// evidence classification and CAB subtype token, the requested amount, and the prior/final values with
    /// the final delta. Produced only by <see cref="OperationEvaluator"/>, which supplies
    /// <c>FinalDelta = FinalValue − PriorValue</c>; nothing else constructs it.
    /// </summary>
    /// <param name="Register">The target Core register.</param>
    /// <param name="OperationType">The evidence classification of the operation's mechanical meaning.</param>
    /// <param name="Subtype">The CAB-verbatim quantity subtype token.</param>
    /// <param name="RequestedAmount">The requested delta (Add/Multiply) or proposed value (Assign).</param>
    /// <param name="PriorValue">The register value before the operation.</param>
    /// <param name="FinalValue">The register value the operation resolves to.</param>
    /// <param name="FinalDelta">The resolved change, equal to FinalValue − PriorValue.</param>
    public sealed record EvaluatedOperation(
        CoreRegister Register,
        QuantityOperationType OperationType,
        string Subtype,
        int RequestedAmount,
        int PriorValue,
        int FinalValue,
        int FinalDelta
    );
}