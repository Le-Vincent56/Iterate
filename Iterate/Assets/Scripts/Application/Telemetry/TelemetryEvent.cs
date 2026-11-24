using System;
using Iterate.Application.Content.Json;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// One telemetry line before encoding: the UX §28 envelope and the event's type-specific data.
    /// The backend never records an input method, so the encoded <c>input</c> member is always null.
    /// </summary>
    /// <param name="Run">The run identity.</param>
    /// <param name="Sequence">The zero-based line number within the run.</param>
    /// <param name="Type">The stable event label, one of <see cref="TelemetryEventTypes"/>.</param>
    /// <param name="At">The UTC time the line was written.</param>
    /// <param name="Milliseconds">Monotonic milliseconds since the run began.</param>
    /// <param name="Build">The build version.</param>
    /// <param name="UX">The UX source version.</param>
    /// <param name="Content">The catalog revision.</param>
    /// <param name="Mode">The Session mode, <c>onboarding</c> or <c>ordinary</c>.</param>
    /// <param name="Stage">The Session stage after the event.</param>
    /// <param name="Process">The current Process identity, or null.</param>
    /// <param name="Execution">The executions run in the current Process, or null.</param>
    /// <param name="Subject">The command name or the event's subject, or null.</param>
    /// <param name="Target">The target by data identity, or null.</param>
    /// <param name="Result">The command result or the event's outcome, or null.</param>
    /// <param name="Reason">The rejection name, or null.</param>
    /// <param name="Resources">The resources after the event, or null outside a Session.</param>
    /// <param name="Configuration">The current configuration snapshot identity, or null.</param>
    /// <param name="Data">The type-specific members; empty, never null, when there are none.</param>
    public sealed record TelemetryEvent(
        string Run,
        int Sequence,
        string Type,
        DateTimeOffset At,
        long Milliseconds,
        string Build,
        string UX,
        string Content,
        string Mode,
        string Stage,
        string Process,
        int? Execution,
        string Subject,
        string Target,
        string Result,
        string Reason,
        TelemetryResources Resources,
        string Configuration,
        JsonObject Data
    );
}