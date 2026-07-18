namespace Iterate.Domain.Values
{
    /// <summary>
    /// Represents a quantity of Tokens constrained to zero or higher.
    /// </summary>
    /// <param name="Value">The raw Tokens value; never negative after construction.</param>
    public readonly record struct TokenAmount(int Value)
    {
        private const int MinimumTokens = 0;

        public int Value { get; init; } = StayAboveMinimum(Value);

        /// <summary>
        /// Whether no Tokens remain to spend.
        /// </summary>
        public bool IsExhausted => Value <= MinimumTokens;

        public static explicit operator int(TokenAmount tokens) => tokens.Value;

        public static explicit operator TokenAmount(int value) => new(value);

        public static TokenAmount operator -(TokenAmount left, int right)
        {
            return new TokenAmount(left.Value - right);
        }

        public static TokenAmount operator +(TokenAmount left, int right)
        {
            return new TokenAmount(left.Value + right);
        }

        public static bool operator <(TokenAmount left, TokenAmount right) => left.Value < right.Value;

        public static bool operator <=(TokenAmount left, TokenAmount right) => left.Value <= right.Value;

        public static bool operator >(TokenAmount left, TokenAmount right) => left.Value > right.Value;

        public static bool operator >=(TokenAmount left, TokenAmount right) => left.Value >= right.Value;

        /// <summary>
        /// Clamps a raw value to the minimum allowed Tokens quantity.
        /// </summary>
        /// <param name="value">The raw candidate value.</param>
        /// <returns>The value, or the minimum when the value falls below it.</returns>
        private static int StayAboveMinimum(int value)
        {
            return value < MinimumTokens
                ? MinimumTokens
                : value;
        }
    }
}