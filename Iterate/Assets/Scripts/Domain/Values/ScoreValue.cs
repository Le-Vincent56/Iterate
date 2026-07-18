namespace Iterate.Domain.Values
{
    /// <summary>
    /// The Score register: the final output produced by an execution.
    /// Carries no floor or ceiling; range semantics belong to the runtime rules that mutate it.
    /// </summary>
    /// <param name="Value">The raw register value; may be negative.</param>
    public readonly record struct ScoreValue(int Value)
    {
        public static explicit operator int(ScoreValue register) => register.Value;

        public static explicit operator ScoreValue(int value) => new(value);

        public static bool operator <(ScoreValue left, ScoreValue right) => left.Value < right.Value;

        public static bool operator <=(ScoreValue left, ScoreValue right) => left.Value <= right.Value;

        public static bool operator >(ScoreValue left, ScoreValue right) => left.Value > right.Value;

        public static bool operator >=(ScoreValue left, ScoreValue right) => left.Value >= right.Value;
    }
}