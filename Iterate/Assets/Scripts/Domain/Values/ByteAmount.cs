namespace Iterate.Domain.Values
{
    /// <summary>
    /// Represents a quantity of Bytes constrained to zero or higher.
    /// </summary>
    /// <param name="Value">The raw Bytes value; never negative after construction.</param>
    public readonly record struct ByteAmount(int Value)
    {
        private const int MinimumBytes = 0;

        public int Value { get; init; } = StayAboveMinimum(Value);

        /// <summary>
        /// Whether no Bytes remain to spend.
        /// </summary>
        public bool IsExhausted => Value <= MinimumBytes;

        public static explicit operator int(ByteAmount bytes) => bytes.Value;

        public static explicit operator ByteAmount(int value) => new(value);

        public static ByteAmount operator -(ByteAmount left, int right)
        {
            return new ByteAmount(left.Value - right);
        }

        public static ByteAmount operator +(ByteAmount left, int right)
        {
            return new ByteAmount(left.Value + right);
        }

        public static bool operator <(ByteAmount left, ByteAmount right) => left.Value < right.Value;

        public static bool operator <=(ByteAmount left, ByteAmount right) => left.Value <= right.Value;

        public static bool operator >(ByteAmount left, ByteAmount right) => left.Value > right.Value;

        public static bool operator >=(ByteAmount left, ByteAmount right) => left.Value >= right.Value;

        /// <summary>
        /// Clamps a raw value to the minimum allowed Bytes quantity.
        /// </summary>
        /// <param name="value">The raw candidate value.</param>
        /// <returns>The value, or the minimum when the value falls below it.</returns>
        private static int StayAboveMinimum(int value)
        {
            return value < MinimumBytes
                ? MinimumBytes
                : value;
        }
    }
}