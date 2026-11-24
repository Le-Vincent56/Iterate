using System;
using System.Collections.Generic;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Reads Session Flow's context into configuration and Buffer snapshots, as 6.i's view mappers
    /// do, because the copied views carry no Patch attachments or Structure membership.
    /// </summary>
    public sealed class TelemetrySnapshotMapper
    {
        private readonly SessionFlow.SessionFlow _flow;

        /// <summary>
        /// Creates the mapper over Session Flow.
        /// </summary>
        /// <param name="flow">The Session Flow whose context is read.</param>
        /// <exception cref="ArgumentNullException">Thrown when Session Flow is null.</exception>
        public TelemetrySnapshotMapper(SessionFlow.SessionFlow flow)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        /// <summary>
        /// Snapshots the open Process's configuration.
        /// </summary>
        /// <returns>The snapshot, or null when no Process is open.</returns>
        public ConfigurationSnapshot Configuration()
        {
            SessionContext context = _flow.Context;
            BuildState build = context.Build;
            if (context.Process == null || build == null)
                return null;

            IReadOnlyList<SourceSlot> slots = build.CurrentArrangement.Slots;
            List<ConfigurationLine> lines = new(slots.Count);
            List<ConfigurationPatch> patches = new();
            for (int index = 0; index < slots.Count; index++)
            {
                SourceSlot slot = slots[index];
                lines.Add(LineOf(slot));
                if (slot.Instruction != null)
                    AddPatches(patches, slot.Instruction);
            }

            IReadOnlyList<DirectiveInstance> pending = build.PendingPragmas;
            List<ConfigurationPragma> pragmas = new(pending.Count);
            for (int index = 0; index < pending.Count; index++)
                pragmas.Add(new ConfigurationPragma(pending[index].InstanceID, pending[index].Definition.ID.Value));

            return new ConfigurationSnapshot(lines, patches, pragmas, build.Classify());
        }

        /// <summary>
        /// Snapshots the open Process's Instruction Buffer.
        /// </summary>
        /// <returns>The snapshot, or null when no Process is open.</returns>
        public BufferSnapshot Buffer()
        {
            ProcessState process = _flow.Context.Process;
            if (process == null)
                return null;

            InstructionBuffer buffer = process.Buffer;
            IReadOnlyList<BufferSlot> slots = buffer.Slots;
            List<BufferSlotView> slotViews = new(slots.Count);
            for (int index = 0; index < slots.Count; index++)
            {
                RepositoryItem item = slots[index].Item;
                slotViews.Add(new BufferSlotView(slots[index].Number, item?.InstanceID, item?.DefinitionID));
            }

            return new BufferSnapshot(
                buffer.Capacity,
                slotViews,
                buffer.IncomingItem?.InstanceID,
                new List<InstanceID>(process.ExposedInstanceIDs),
                buffer.PendingArrivals.Count
            );
        }

        /// <summary>
        /// Projects one source line.
        /// </summary>
        /// <param name="slot">The source slot.</param>
        /// <returns>The line by data identity.</returns>
        private static ConfigurationLine LineOf(SourceSlot slot)
        {
            int line = slot.Position.LineNumber;
            if (slot.Instruction != null)
                return new ConfigurationLine(line, slot.Kind, slot.Instruction.InstanceID, slot.Instruction.Definition.ID.Value, slot.Structure?.InstanceID);

            if (slot.Kind == SourceSlotKind.StructureHeader)
                return new ConfigurationLine(line, slot.Kind, slot.Structure.InstanceID, slot.Structure.Definition.ID.Value, null);

            if (slot.Core != null)
                return new ConfigurationLine(line, slot.Kind, null, slot.Core.Identity, null);

            return new ConfigurationLine(line, slot.Kind, null, null, slot.Structure?.InstanceID);
        }

        /// <summary>
        /// Adds an installed Instruction's attached Patches.
        /// </summary>
        /// <param name="patches">The list to add to.</param>
        /// <param name="host">The installed Instruction.</param>
        private static void AddPatches(List<ConfigurationPatch> patches, InstructionInstance host)
        {
            IReadOnlyList<PatchAttachment> attached = host.AttachedPatches;
            for (int index = 0; index < attached.Count; index++)
            {
                PatchInstance patch = attached[index].Patch;
                patches.Add(new ConfigurationPatch(patch.InstanceID, patch.Definition.ID.Value, host.InstanceID, attached[index].Socket));
            }
        }
    }
}