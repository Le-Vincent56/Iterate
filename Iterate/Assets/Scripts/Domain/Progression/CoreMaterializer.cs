using System;
using System.Collections.Generic;
using System.Globalization;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Turns an authored Core into the arrangement a Process opens with: one immutable Core slot per
    /// fixed instruction line and one empty slot per open position, and **nothing else**. Progression
    /// never builds an Instruction or Structure slot — installing content is Compilation's edit, which
    /// keeps one owner for install semantics. A Core-owned fixed Structure is refused rather than
    /// approximated, because the Done Core model cannot hold one.
    /// </summary>
    public static class CoreMaterializer
    {
        /// <summary>
        /// Materialises a Core definition.
        /// </summary>
        /// <param name="core">The authored Core.</param>
        /// <returns>The materialisation, or a typed rejection.</returns>
        public static CoreMaterialization Materialize(CoreDefinition core)
        {
            if (core == null)
                throw new ArgumentNullException(nameof(core));

            List<SourceSlot> slots = new(core.Lines.Count);
            for (int index = 0; index < core.Lines.Count; index++)
            {
                CoreLineSpec line = core.Lines[index];
                SourcePosition position = new(line.Position);

                if (line.Kind == CoreLineKind.FixedStructure)
                    return CoreMaterialization.Rejected(ProcessCreationRejection.CoreStructureUnsupported);

                if (line.Kind == CoreLineKind.Open)
                {
                    slots.Add(SourceSlot.ForEmpty(position));
                    continue;
                }

                slots.Add(SourceSlot.ForCore(position, new CoreLine(Identity(core, line.Position), line.Operation)));
            }

            return CoreMaterialization.Success(new SourceArrangement(slots), core.FinalOutputPosition);
        }

        /// <summary>
        /// Builds a Core line's stable identity from its Core and position, so a line is attributable
        /// across a replay without depending on list order.
        /// </summary>
        /// <param name="core">The owning Core.</param>
        /// <param name="position">The line's one-based position.</param>
        /// <returns>The line identity.</returns>
        private static string Identity(CoreDefinition core, int position)
        {
            return core.ID.Value + ":L" + position.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}