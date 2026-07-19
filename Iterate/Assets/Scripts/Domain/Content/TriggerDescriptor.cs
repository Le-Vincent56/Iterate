using System.Collections.Generic;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// Describes what an effect observes to fire: an event family and controlled subtype, controlled
    /// qualifiers, and the timing band or named boundary at which observation occurs.
    /// </summary>
    /// <param name="EventFamily">The controlled event family observed.</param>
    /// <param name="EventSubtype">The controlled event-subtype token within the family.</param>
    /// <param name="Qualifiers">The controlled qualifiers narrowing the match.</param>
    /// <param name="Timing">The timing band or named boundary of observation.</param>
    public sealed record TriggerDescriptor(
        EventFamily EventFamily,
        string EventSubtype,
        IReadOnlyList<TriggerQualifier> Qualifiers,
        EffectTiming Timing
    );
}