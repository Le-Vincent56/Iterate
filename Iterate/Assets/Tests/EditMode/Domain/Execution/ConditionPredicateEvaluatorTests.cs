using System;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the snapshot-only Condition predicate evaluator: parity for <c>IS_EVEN</c> with the
    /// operand ignored, the inclusive boundary for <c>AT_LEAST</c>, and the null-predicate guard.
    /// </summary>
    public sealed class ConditionPredicateEvaluatorTests
    {
        [Test]
        public void IsEven_EvenValue_IsTrue()
        {
            Assert.IsTrue(ConditionPredicateEvaluator.Evaluate(Predicate(PredicateComparison.IsEven, 0), 4));
        }

        [Test]
        public void IsEven_OddValue_IsFalse()
        {
            Assert.IsFalse(ConditionPredicateEvaluator.Evaluate(Predicate(PredicateComparison.IsEven, 0), 3));
        }

        [Test]
        public void IsEven_Zero_IsTrue()
        {
            Assert.IsTrue(ConditionPredicateEvaluator.Evaluate(Predicate(PredicateComparison.IsEven, 0), 0));
        }

        [Test]
        public void IsEven_NegativeEvenValue_IsTrue()
        {
            Assert.IsTrue(ConditionPredicateEvaluator.Evaluate(Predicate(PredicateComparison.IsEven, 0), -2));
        }

        [Test]
        public void IsEven_OperandIsIgnored()
        {
            Assert.IsTrue(ConditionPredicateEvaluator.Evaluate(Predicate(PredicateComparison.IsEven, 7), 4));
        }

        [Test]
        public void AtLeast_ValueOnTheBoundary_IsTrue()
        {
            Assert.IsTrue(ConditionPredicateEvaluator.Evaluate(Predicate(PredicateComparison.AtLeast, 3), 3));
        }

        [Test]
        public void AtLeast_ValueAboveTheBoundary_IsTrue()
        {
            Assert.IsTrue(ConditionPredicateEvaluator.Evaluate(Predicate(PredicateComparison.AtLeast, 3), 4));
        }

        [Test]
        public void AtLeast_ValueBelowTheBoundary_IsFalse()
        {
            Assert.IsFalse(ConditionPredicateEvaluator.Evaluate(Predicate(PredicateComparison.AtLeast, 3), 2));
        }

        [Test]
        public void NullPredicate_Throws()
        {
            Assert.Throws<ArgumentException>(() => ConditionPredicateEvaluator.Evaluate(null, 0));
        }

        /// <summary>
        /// Builds a Signal predicate with the given comparison and operand.
        /// </summary>
        /// <param name="comparison">The predicate comparison.</param>
        /// <param name="operand">The comparison operand.</param>
        /// <returns>The predicate.</returns>
        private static StructurePredicate Predicate(PredicateComparison comparison, int operand)
        {
            return new StructurePredicate(CoreRegister.Signal, comparison, operand);
        }
    }
}
