using System;
using Iterate.Domain.Content;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// Evaluates a Condition predicate against its captured register snapshot — never against live
    /// state. <c>IS_EVEN</c> is parity over the snapshot with the operand ignored; <c>AT_LEAST</c> is
    /// the inclusive lower bound against the operand.
    /// </summary>
    public static class ConditionPredicateEvaluator
    {
        /// <summary>
        /// Evaluates the predicate against the snapshot value.
        /// </summary>
        /// <param name="predicate">The frozen Condition predicate.</param>
        /// <param name="snapshotValue">The register value captured at Structure entry.</param>
        /// <returns>True when the snapshot satisfies the predicate.</returns>
        /// <exception cref="ArgumentException">Thrown when the predicate is null or its comparison unknown.</exception>
        public static bool Evaluate(StructurePredicate predicate, int snapshotValue)
        {
            if (predicate == null)
                throw new ArgumentException("Evaluation requires a predicate.", nameof(predicate));

            return predicate.Comparison switch
            {
                PredicateComparison.IsEven => snapshotValue % 2 == 0,
                PredicateComparison.AtLeast => snapshotValue >= predicate.Operand,
                _ => throw new ArgumentException($"Unknown predicate comparison {predicate.Comparison}.",
                    nameof(predicate))
            };
        }
    }
}