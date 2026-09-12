using System;
using Iterate.Domain.Compilation;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// One authored Core line: its Core position paired with exactly the payload its kind admits. A
    /// fixed instruction carries an operation and nothing else; an open position carries no payload
    /// at all; a fixed Structure carries a predicate and the one fixed instruction it contains. A
    /// contained line carries position zero, because it occupies no Core position of its own.
    /// </summary>
    /// <param name="Position">The one-based Core position; zero for a contained line.</param>
    /// <param name="Kind">Which line kind this spec declares.</param>
    /// <param name="Operation">The operation a fixed instruction applies; null for every other kind.</param>
    /// <param name="Predicate">The predicate a fixed Structure evaluates; null for every other kind.</param>
    /// <param name="Contained">The fixed instruction a fixed Structure contains; null for every other kind.</param>
    public sealed record CoreLineSpec(
        int Position,
        CoreLineKind Kind,
        CoreLineOperation Operation,
        StructurePredicate Predicate,
        CoreLineSpec Contained
    )
    {
        /// <summary>
        /// The one-based Core position; zero for a contained line. Validated non-negative at
        /// construction.
        /// </summary>
        public int Position { get; } = RequirePosition(Position);

        /// <summary>
        /// The operation a fixed instruction applies; null for every other kind. Validated against
        /// the line kind's whole field shape at construction.
        /// </summary>
        public CoreLineOperation Operation { get; } = RequireShape(
            Kind,
            Operation,
            Predicate,
            Contained
        );

        /// <summary>
        /// Validates the Core position.
        /// </summary>
        /// <param name="position">The candidate position.</param>
        /// <returns>The position unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the position is negative.</exception>
        private static int RequirePosition(int position)
        {
            if (position < 0)
                throw new ArgumentException("A CoreLineSpec requires a position of zero or greater.", nameof(position));

            return position;
        }

        /// <summary>
        /// Validates that a Core line carries exactly the payload its kind admits and returns the
        /// operation unchanged.
        /// </summary>
        /// <param name="kind">The declared line kind.</param>
        /// <param name="operation">The candidate operation.</param>
        /// <param name="predicate">The candidate predicate.</param>
        /// <param name="contained">The candidate contained line.</param>
        /// <returns>The operation unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the fields do not match the kind's shape.</exception>
        private static CoreLineOperation RequireShape(
            CoreLineKind kind,
            CoreLineOperation operation,
            StructurePredicate predicate,
            CoreLineSpec contained
        )
        {
            switch (kind)
            {
                case CoreLineKind.FixedInstruction when operation == null:
                    throw new ArgumentException("A fixed-instruction Core line requires an operation.", nameof(operation));
                
                case CoreLineKind.FixedInstruction when predicate != null:
                    throw new ArgumentException("A fixed-instruction Core line must not carry a predicate.", nameof(predicate));
                
                case CoreLineKind.FixedInstruction when contained != null:
                    throw new ArgumentException("A fixed-instruction Core line must not carry a contained line.", nameof(contained));
                
                case CoreLineKind.FixedInstruction:
                    return operation;
                
                case CoreLineKind.Open when operation != null:
                    throw new ArgumentException("An open Core line must not carry an operation.", nameof(operation));
                
                case CoreLineKind.Open when predicate != null:
                    throw new ArgumentException("An open Core line must not carry a predicate.", nameof(predicate));
                
                case CoreLineKind.Open when contained != null:
                    throw new ArgumentException("An open Core line must not carry a contained line.", nameof(contained));
                
                case CoreLineKind.Open:
                    return operation;
            }

            if (operation != null)
                throw new ArgumentException("A fixed-Structure Core line must not carry an operation.", nameof(operation));

            if (predicate == null)
                throw new ArgumentException("A fixed-Structure Core line requires a predicate.", nameof(predicate));

            if (contained == null)
                throw new ArgumentException("A fixed-Structure Core line requires a contained line.", nameof(contained));

            if (contained.Kind != CoreLineKind.FixedInstruction)
                throw new ArgumentException("A fixed-Structure Core line contains a fixed instruction.", nameof(contained));

            return operation;
        }
    }
}