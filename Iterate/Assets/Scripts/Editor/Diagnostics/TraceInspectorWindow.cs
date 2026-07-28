using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;
using Iterate.Domain.Diagnostics;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Infrastructure.Content;
using InstanceID = Iterate.Domain.Values.InstanceID;

namespace Iterate.Editor.Diagnostics
{
    /// <summary>
    /// The trace debugger: a read-only window over one execution record, leading with the question a
    /// developer actually brings to a trace — why did this effect fire, or why did it not. It renders
    /// the effect-resolution projections the Domain layer derives and tested; it derives nothing itself,
    /// which is what keeps an untested Editor surface honest.
    /// Records come from the diagnostic fixture registry, generated in-session by running a fixture
    /// through a window-owned builder and scheduler, or from any caller handing one to
    /// Show(ExecutionRecord). Nothing is loaded from or written to disk: no record serialization exists.
    /// Strictly read-only — the window never constructs, edits, or re-freezes a record.
    /// </summary>
    public sealed class TraceInspectorWindow : EditorWindow
    {
        private const string CatalogFolder = "Catalog";

        private const string SilenceCaveat =
            "Absence here is not proof of ineligibility. An effect that was never a structural candidate, "
            + "and one whose allowance was already consumed, both emit no evidence at all — so neither "
            + "appears below. This view answers \"it near-missed, and on which requirement\"; it cannot "
            + "answer \"it was never a candidate\".";

        private ExecutionRecord _record;
        private TraceCausality _causality;
        private TraceEventID? _selected;
        private string _fixture;
        private ScrollView _body;
        private Label _status;

        /// <summary>
        /// Opens the debugger with no record loaded.
        /// </summary>
        [MenuItem("Iterate/Trace Debugger")]
        public static void Open()
        {
            GetWindow<TraceInspectorWindow>("Trace Debugger");
        }

        /// <summary>
        /// Opens the debugger on a record the caller already holds — the hand-off entry point for any
        /// future producer of records.
        /// </summary>
        /// <param name="record">The frozen record to browse.</param>
        public static void Show(ExecutionRecord record)
        {
            if (record == null)
                throw new ArgumentException("The debugger requires a record.", nameof(record));

            TraceInspectorWindow window = GetWindow<TraceInspectorWindow>("Trace Debugger");
            window.Load(record, "handed record");
        }

        /// <summary>
        /// Builds the persistent chrome — the fixture toolbar, the status line, and the scrolling body —
        /// then renders whatever record is currently loaded.
        /// </summary>
        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            Toolbar toolbar = new();
            IReadOnlyList<string> names = DiagnosticFixtureRegistry.Names();
            List<string> choices = new(names);
            _fixture ??= choices[0];

            PopupField<string> picker = new("Fixture", choices, _fixture);
            picker.RegisterValueChangedCallback(changed => _fixture = changed.newValue);
            toolbar.Add(picker);

            ToolbarButton generate = new(GenerateSelectedFixture) { text = "Generate & Debug" };
            toolbar.Add(generate);
            root.Add(toolbar);

            _status = new Label("No record loaded.");
            _status.style.paddingLeft = 6;
            _status.style.paddingTop = 4;
            root.Add(_status);

            _body = new ScrollView(ScrollViewMode.Vertical);
            _body.style.flexGrow = 1;
            root.Add(_body);

            Rebuild();
        }

        /// <summary>
        /// Loads the shipped catalog, builds the selected fixture, runs it on a window-owned builder and
        /// scheduler, and opens the resulting record. A fixture that cannot be built is reported as a
        /// fixture defect, which is a different thing from a diagnostic finding.
        /// </summary>
        private void GenerateSelectedFixture()
        {
            string root = Path.Combine(UnityEngine.Application.streamingAssetsPath, CatalogFolder);
            if (!Directory.Exists(root))
            {
                Debug.LogError("[Trace] The catalog folder was not found at " + root + ".");
                return;
            }

            CatalogLoader loader = new(
                new CatalogJsonReader(),
                new CatalogValidator(),
                new CatalogFreezer(),
                new CatalogDirectorySource(root)
            );

            try
            {
                ContentCatalog catalog = Task.Run(() => loader.LoadAsync(CancellationToken.None)).GetAwaiter().GetResult();
                ExecutionRequest request = DiagnosticFixtureRegistry.Build(_fixture, catalog);
                ExecutionRecord produced = new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
                Load(produced, _fixture);
            }
            catch (CatalogLoadException exception)
            {
                Debug.LogError("[Trace] The catalog failed to load: " + exception.Errors.Count + " error(s).");
            }
            catch (ArgumentException exception)
            {
                Debug.LogError("[Trace] Fixture defect in '" + _fixture + "': " + exception.Message);
            }
        }

        /// <summary>
        /// Adopts a record and rebuilds every view over it.
        /// </summary>
        /// <param name="record">The record to browse.</param>
        /// <param name="source">A short description of where it came from.</param>
        private void Load(ExecutionRecord record, string source)
        {
            _record = record;
            _causality = EffectResolutionProjection.CausalityOf(record);
            _selected = null;

            if (_status != null)
            {
                _status.text = "Loaded " + source + " — " + record.Events.Count + " events, "
                    + record.Units.Count + " units, completion " + record.CompletionStatus
                    + ", safety " + record.SafetyStatus + ".";
            }

            Rebuild();
        }

        /// <summary>
        /// Rebuilds every view from the current record and selection. Rendering is regenerated wholesale
        /// rather than patched, because a read-only projection has no state worth reconciling.
        /// </summary>
        private void Rebuild()
        {
            if (_body == null)
                return;

            _body.Clear();

            if (_record == null)
            {
                _body.Add(new Label("Pick a fixture and choose Generate & Debug, or hand a record to Show."));
                return;
            }

            _body.Add(Caveat());
            _body.Add(TimelinesSection());
            _body.Add(OffersSection());
            _body.Add(CausalSection());
            _body.Add(HeaderSection());
            _body.Add(UnitSection());
            _body.Add(EventSection());
            _body.Add(EvidenceSection());
            _body.Add(SafetySection());
        }

        /// <summary>
        /// The fixed help text stating what absence in these views does and does not prove.
        /// </summary>
        /// <returns>The caveat box.</returns>
        private static VisualElement Caveat()
        {
            HelpBox box = new(SilenceCaveat, HelpBoxMessageType.Info);
            box.style.marginTop = 4;
            box.style.marginBottom = 4;
            return box;
        }

        /// <summary>
        /// One row per effect origin: its qualifications, commitments, and near-misses in order, each
        /// near-miss showing its failed requirement inline. Selecting an entry selects its event.
        /// </summary>
        /// <returns>The section.</returns>
        private VisualElement TimelinesSection()
        {
            IReadOnlyList<EffectResolutionTimeline> timelines = EffectResolutionProjection.TimelinesOf(_record);
            Foldout section = Section("Effect timelines (" + timelines.Count + ")");

            if (timelines.Count == 0)
                section.Add(new Label("No effect resolved or near-missed in this execution."));

            for (int index = 0; index < timelines.Count; index++)
            {
                EffectResolutionTimeline timeline = timelines[index];
                string host = timeline.Host == null ? "no host" : "host " + timeline.Host.Value.Value;
                Foldout origin = Section("origin " + timeline.Origin.Value + " (" + host + ") — " + timeline.Entries.Count + " entries");

                for (int entry = 0; entry < timeline.Entries.Count; entry++)
                {
                    origin.Add(EntryRow(timeline.Entries[entry]));
                }

                section.Add(origin);
            }

            return section;
        }

        /// <summary>
        /// One row per candidate event: who qualified, who committed, and who near-missed on what.
        /// </summary>
        /// <returns>The section.</returns>
        private VisualElement OffersSection()
        {
            IReadOnlyList<BoundaryOfferView> offers = EffectResolutionProjection.OffersOf(_record);
            Foldout section = Section("Boundary offers (" + offers.Count + ")");

            if (offers.Count == 0)
                section.Add(new Label("No candidate event drew a resolution in this execution."));

            for (int index = 0; index < offers.Count; index++)
            {
                BoundaryOfferView offer = offers[index];
                Foldout candidate = Section("candidate event " + offer.CandidateEvent.Value + " — " + offer.Entries.Count + " observers");

                for (int entry = 0; entry < offer.Entries.Count; entry++)
                {
                    candidate.Add(EntryRow(offer.Entries[entry]));
                }

                section.Add(candidate);
            }

            return section;
        }

        /// <summary>
        /// The selected event's ancestor chain, direct consequences, and containment children.
        /// </summary>
        /// <returns>The section.</returns>
        private VisualElement CausalSection()
        {
            Foldout section = Section("Causal chain");

            if (_selected == null)
            {
                section.Add(new Label("Select a resolution entry or an event to walk its chain."));
                return section;
            }

            TraceEventID identity = _selected.Value;
            section.Add(new Label("selected: event " + identity.Value));
            section.Add(new Label("ancestors (nearest first): " + Join(_causality.AncestorsOf(identity))));
            section.Add(new Label("direct consequences: " + Join(_causality.ConsequencesOf(identity))));
            section.Add(new Label("containment children: " + Join(_causality.ContainmentChildrenOf(identity))));
            return section;
        }

        /// <summary>
        /// The reproduction header: identities, revision stamps, instance lists, and the initial state.
        /// </summary>
        /// <returns>The section.</returns>
        private VisualElement HeaderSection()
        {
            ExecutionEvidenceHeader header = _record.Header;
            Foldout section = Section("Header");
            section.Add(new Label("execution: " + header.ExecutionIdentity));
            section.Add(new Label("compilation: " + header.CompilationIdentity + " | source revision: " + header.CompiledSourceRevision));
            section.Add(new Label("Process: " + header.ProcessIdentity + " | Core: " + header.CoreIdentity));
            section.Add(new Label("rule configuration: " + header.ProcessRuleConfigurationIdentity + " | Session seed: " + header.SessionSeedIdentity));

            for (int index = 0; index < header.RevisionStamps.Count; index++)
            {
                RevisionStamp stamp = header.RevisionStamps[index];
                section.Add(new Label("stamp: " + stamp.Name + " = " + stamp.Revision));
            }

            section.Add(new Label("active Directives: " + JoinInstances(header.ActiveDirectiveInstances)));
            section.Add(new Label("installed Dependencies: " + JoinInstances(header.InstalledDependencyInstances)));
            section.Add(new Label("relevant Patches: " + JoinInstances(header.RelevantPatchInstances)));
            section.Add(new Label("initial: Value " + header.InitialState.InitialValue.Value
                + " Signal " + header.InitialState.InitialSignal.Value
                + " Score " + header.InitialState.InitialScore.Value));
            return section;
        }

        /// <summary>
        /// The unit tree: parentless roots with their descendants nested, each labelled with its
        /// activation and closure. Selecting a unit is not offered — units are browsed, events are
        /// selected, because causality is an event relation.
        /// </summary>
        /// <returns>The section.</returns>
        private VisualElement UnitSection()
        {
            Foldout section = Section("Units (" + _record.Units.Count + ")");
            HashSet<int> descendants = new();

            for (int index = 0; index < _record.Units.Count; index++)
            {
                IReadOnlyList<RuntimeUnitID> children = _record.Units[index].DescendantUnits;
                for (int child = 0; child < children.Count; child++)
                {
                    descendants.Add(children[child].Value);
                }
            }

            for (int index = 0; index < _record.Units.Count; index++)
            {
                RuntimeUnitRecord unit = _record.Units[index];
                if (descendants.Contains(unit.Identity.Value))
                    continue;

                section.Add(UnitRow(unit, 0));
            }

            return section;
        }

        /// <summary>
        /// One unit's row and, nested beneath it, the rows of its descendant units.
        /// </summary>
        /// <param name="unit">The unit to render.</param>
        /// <param name="depth">The nesting depth, used only for indentation.</param>
        /// <returns>The row and its nested descendants.</returns>
        private VisualElement UnitRow(RuntimeUnitRecord unit, int depth)
        {
            VisualElement container = new();
            Label label = new(Indent(depth) + "unit " + unit.Identity.Value
                + " | " + unit.Opening.Activation
                + " | closure " + unit.Closure.Status
                + " | disposition " + unit.Closure.FinalDisposition
                + " | events " + unit.ChildEvents.Count);
            container.Add(label);

            for (int index = 0; index < unit.DescendantUnits.Count; index++)
            {
                RuntimeUnitRecord child = UnitOf(unit.DescendantUnits[index]);
                if (child != null)
                    container.Add(UnitRow(child, depth + 1));
            }

            return container;
        }

        /// <summary>
        /// The chronological event list. Each row selects its event for the causal and evidence panes.
        /// </summary>
        /// <returns>The section.</returns>
        private VisualElement EventSection()
        {
            Foldout section = Section("Events (" + _record.Events.Count + ")");

            for (int index = 0; index < _record.Events.Count; index++)
            {
                EventRecord entry = _record.Events[index];
                Button row = new(() => Select(entry.Identity))
                {
                    text = entry.Identity.Value + "  " + entry.Evidence.Family + " / " + entry.Evidence.Subtype
                        + (entry.Evidence.Disposition == null ? string.Empty : "  [" + entry.Evidence.Disposition + "]")
                };
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                section.Add(row);
            }

            return section;
        }

        /// <summary>
        /// The full evidence of the selected event, including its payload rendered per concrete type
        /// with an unknown-safe default, so an added payload type degrades to a named line rather than
        /// silently disappearing.
        /// </summary>
        /// <returns>The section.</returns>
        private VisualElement EvidenceSection()
        {
            Foldout section = Section("Event evidence");

            if (_selected == null)
            {
                section.Add(new Label("Select an event."));
                return section;
            }

            EventRecord? found = EventOf(_selected.Value);
            if (found == null)
            {
                section.Add(new Label("The selected event is not in this record."));
                return section;
            }

            EventRecord entry = found.Value;

            EventEvidence evidence = entry.Evidence;
            section.Add(new Label("family / subtype: " + evidence.Family + " / " + evidence.Subtype));
            section.Add(new Label("causal depth: " + evidence.CausalDepth));
            section.Add(new Label("containing unit: " + Optional(evidence.ContainingUnit)));
            section.Add(new Label("parent event: " + Optional(evidence.ParentEvent)));
            section.Add(new Label("causing event: " + Optional(evidence.CausingEvent)));
            section.Add(new Label("effect origin: " + Optional(evidence.EffectOriginInstance)));
            section.Add(new Label("host instance: " + Optional(evidence.HostInstance)));
            section.Add(new Label("Core line: " + (evidence.CoreLineIdentity ?? "—")));
            section.Add(new Label("target: " + (evidence.TargetIdentity ?? "—")));
            section.Add(new Label("ownership: " + (evidence.Ownership == null ? "—" : evidence.Ownership.ToString())));
            section.Add(new Label("disposition: " + (evidence.Disposition == null ? "—" : evidence.Disposition.ToString())));
            section.Add(new Label("disposition reason: " + (evidence.DispositionReason ?? "—")));
            section.Add(new Label("safety status: " + evidence.SafetyStatus));
            section.Add(new Label("lineage: " + JoinInstances(evidence.Lineage.Entries)));
            section.Add(PayloadView(evidence.Payload));
            return section;
        }

        /// <summary>
        /// Renders one event payload per concrete type. The payload hierarchy is closed to the Domain
        /// assembly, so the default arm is not dead code: it is what a newly added payload degrades to,
        /// a named line rather than a silent disappearance.
        /// </summary>
        /// <param name="payload">The payload, or null when the event carries none.</param>
        /// <returns>The rendered payload line.</returns>
        private static VisualElement PayloadView(EventPayload payload)
        {
            switch (payload)
            {
                case null:
                    return new Label("payload: —");

                case QuantityChangePayload quantity:
                    return new Label("payload: quantity " + quantity.QuantityIdentity
                        + " | " + quantity.Category + " " + quantity.Operation
                        + " | requested " + quantity.RequestedAmount
                        + " | prior " + quantity.PriorValue
                        + " | delta " + quantity.FinalDelta
                        + " | final " + quantity.FinalValue
                        + " | modifiers " + quantity.AppliedModifiers.Count
                        + (quantity.AppliedBounds == null ? string.Empty : " | bounded"));

                case SafetyAbortPayload abort:
                    return new Label("payload: safety abort at " + abort.OverLimitOccurrenceIdentity
                        + " | unit " + abort.AffectedUnit.Value
                        + " | breached limits " + abort.BreachedLimits.Count);

                case RandomDecisionPayload:
                    return new Label("payload: random decision (no producer wires random decisions yet)");

                default:
                    return new Label("payload: " + payload.GetType().Name + " (no renderer — add one)");
            }
        }

        /// <summary>
        /// The safety tallies, the four statuses, the derived-history links, and the defect ledger.
        /// </summary>
        /// <returns>The section.</returns>
        private VisualElement SafetySection()
        {
            SafetyCounts counts = _record.SafetyCounts;
            Foldout section = Section("Safety and statuses");
            section.Add(new Label("completion: " + _record.CompletionStatus + " | safety: " + _record.SafetyStatus));
            section.Add(new Label("trace completeness: " + _record.TraceCompleteness
                + " | result validity: " + _record.ResultValidity
                + " | handoff: " + _record.HandoffStatus));
            section.Add(new Label("lineage depth high water: " + counts.LineageDepthHighWater));
            section.Add(new Label("added descendants: " + counts.AddedDescendants));
            section.Add(new Label("source execution units: " + counts.SourceExecutionUnits));
            section.Add(new Label("effect reactions: " + counts.EffectReactions));
            section.Add(new Label("operation transformations: " + counts.OperationTransformations));
            section.Add(new Label("threshold history: " + Join(_record.ThresholdHistory)));
            section.Add(new Label("counter history: " + Join(_record.CounterHistory)));

            Foldout defects = Section("Defect ledger (" + _record.Defects.Count + ")");
            for (int index = 0; index < _record.Defects.Count; index++)
            {
                EvidenceDefect defect = _record.Defects[index];
                defects.Add(new Label(defect.FieldName + " — " + defect.Reason));
            }

            section.Add(defects);
            return section;
        }

        /// <summary>
        /// One resolution entry as a selectable row, showing its kind, the failed requirement when it is
        /// a near-miss, and the candidate it observed.
        /// </summary>
        /// <param name="entry">The resolution entry.</param>
        /// <returns>The row.</returns>
        private Button EntryRow(EffectResolutionEntry entry)
        {
            string requirement = entry.Kind == EffectResolutionEntryKind.FailedToQualify
                ? "  failed on " + entry.FailedRequirement
                : string.Empty;
            string candidate = entry.CausingEvent == null
                ? "  (no candidate)"
                : "  observing event " + entry.CausingEvent.Value.Value;

            Button row = new(() => Select(entry.Event))
            {
                text = "event " + entry.Event.Value + "  " + entry.Kind + requirement + candidate
            };
            row.style.unityTextAlign = TextAnchor.MiddleLeft;
            return row;
        }

        /// <summary>
        /// Selects an event, which drives the causal-chain and evidence panes.
        /// </summary>
        /// <param name="identity">The event to select.</param>
        private void Select(TraceEventID identity)
        {
            _selected = identity;
            Rebuild();
        }

        /// <summary>
        /// Finds a unit by identity.
        /// </summary>
        /// <param name="identity">The unit identity.</param>
        /// <returns>The unit, or null when the record does not hold it.</returns>
        private RuntimeUnitRecord UnitOf(RuntimeUnitID identity)
        {
            for (int index = 0; index < _record.Units.Count; index++)
            {
                if (_record.Units[index].Identity == identity)
                    return _record.Units[index];
            }

            return null;
        }

        /// <summary>
        /// Finds an event by identity.
        /// </summary>
        /// <param name="identity">The event identity.</param>
        /// <returns>The event, or null when the record does not hold it.</returns>
        private EventRecord? EventOf(TraceEventID identity)
        {
            for (int index = 0; index < _record.Events.Count; index++)
            {
                if (_record.Events[index].Identity == identity)
                    return _record.Events[index];
            }

            return null;
        }

        /// <summary>
        /// An expanded foldout used as a section header.
        /// </summary>
        /// <param name="title">The section title.</param>
        /// <returns>The foldout.</returns>
        private static Foldout Section(string title)
        {
            Foldout foldout = new() { text = title, value = true };
            foldout.style.marginTop = 2;
            return foldout;
        }

        /// <summary>
        /// Leading spaces for a nesting depth.
        /// </summary>
        /// <param name="depth">The nesting depth.</param>
        /// <returns>The indent.</returns>
        private static string Indent(int depth)
        {
            return depth == 0 ? string.Empty : new string(' ', depth * 4);
        }

        /// <summary>
        /// Renders a list of event identities, or an em dash when the list is empty.
        /// </summary>
        /// <param name="identities">The event identities.</param>
        /// <returns>The rendered list.</returns>
        private static string Join(IReadOnlyList<TraceEventID> identities)
        {
            if (identities.Count == 0)
                return "—";

            string joined = string.Empty;
            for (int index = 0; index < identities.Count; index++)
            {
                joined += index == 0 ? identities[index].Value.ToString() : ", " + identities[index].Value;
            }

            return joined;
        }

        /// <summary>
        /// Renders a list of instance identities, or an em dash when the list is empty.
        /// </summary>
        /// <param name="instances">The instance identities.</param>
        /// <returns>The rendered list.</returns>
        private static string JoinInstances(IReadOnlyList<InstanceID> instances)
        {
            if (instances.Count == 0)
                return "—";

            string joined = string.Empty;
            for (int index = 0; index < instances.Count; index++)
            {
                joined += index == 0 ? instances[index].Value.ToString() : ", " + instances[index].Value;
            }

            return joined;
        }

        /// <summary>
        /// Renders an optional unit identity, or an em dash when it is not applicable.
        /// </summary>
        /// <param name="identity">The unit identity, or null.</param>
        /// <returns>The rendered identity.</returns>
        private static string Optional(RuntimeUnitID? identity)
        {
            return identity == null ? "—" : identity.Value.Value.ToString();
        }

        /// <summary>
        /// Renders an optional event identity, or an em dash when it is not applicable.
        /// </summary>
        /// <param name="identity">The event identity, or null.</param>
        /// <returns>The rendered identity.</returns>
        private static string Optional(TraceEventID? identity)
        {
            return identity == null ? "—" : identity.Value.Value.ToString();
        }

        /// <summary>
        /// Renders an optional instance identity, or an em dash when it is not applicable.
        /// </summary>
        /// <param name="identity">The instance identity, or null.</param>
        /// <returns>The rendered identity.</returns>
        private static string Optional(InstanceID? identity)
        {
            return identity == null ? "—" : identity.Value.Value.ToString();
        }
    }
}