namespace Iterate.Domain.Values
{
    /// <summary>
    /// The Signal register: the support quantity used to stage, amplify, redirect, or test Value.
    /// Carries no floor or ceiling; range semantics belong to the runtime rules that mutate it.
    /// </summary>
    /// <param name="Value">The raw register value; may be negative.</param>
    public readonly record struct SignalValue(int Value)
    {
        public static explicit operator int(SignalValue register) => register.Value;

        public static explicit operator SignalValue(int value) => new(value);

        public static bool operator <(SignalValue left, SignalValue right) => left.Value < right.Value;

        public static bool operator <=(SignalValue left, SignalValue right) => left.Value <= right.Value;

        public static bool operator >(SignalValue left, SignalValue right) => left.Value > right.Value;

        public static bool operator >=(SignalValue left, SignalValue right) => left.Value >= right.Value;
    }
}