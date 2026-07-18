namespace Iterate.Domain.Values
{
    /// <summary>
    /// The Value register: the primary quantity converted into Score during execution.
    /// Carries no floor or ceiling; range semantics belong to the runtime rules that mutate it.
    /// </summary>
    /// <param name="Value">The raw register value; may be negative.</param>
    public readonly record struct ValueAmount(int Value)
    {
        public static explicit operator int(ValueAmount register) => register.Value;

        public static explicit operator ValueAmount(int value) => new(value);

        public static bool operator <(ValueAmount left, ValueAmount right) => left.Value < right.Value;

        public static bool operator <=(ValueAmount left, ValueAmount right) => left.Value <= right.Value;

        public static bool operator >(ValueAmount left, ValueAmount right) => left.Value > right.Value;

        public static bool operator >=(ValueAmount left, ValueAmount right) => left.Value >= right.Value;
    }
}