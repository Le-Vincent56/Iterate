using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Applies the Build-stage edits through the Build, and archives through the Process so that the
    /// archive observers an installed Dependency declares always fire. Installing onto an occupied
    /// position overwrites the line there.
    /// </summary>
    internal sealed class BuildCommandHandler
    {
        private const ChangedAreas EditAreas = ChangedAreas.Build | ChangedAreas.Buffer;
        private const ChangedAreas ArchiveAreas = ChangedAreas.Buffer | ChangedAreas.Economy;

        /// <summary>
        /// Installs a Buffer item at a source position, overwriting the line there when the position is
        /// occupied.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="InstallLine"/> command.</param>
        /// <returns>The edit result.</returns>
        internal HandlerOutcome Install(SessionContext context, ISessionCommand command)
        {
            InstallLine install = (InstallLine)command;
            BuildState build = context.Build;
            BuildOperationResult edited = IsOccupied(build.CurrentArrangement, install.Position)
                ? build.TryOverwrite(install.Item, install.Position)
                : build.TryInstall(install.Item, install.Position);

            return Edited(edited);
        }

        /// <summary>
        /// Removes the line at a source position.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="RemoveLine"/> command.</param>
        /// <returns>The edit result.</returns>
        internal HandlerOutcome Remove(SessionContext context, ISessionCommand command)
        {
            return Edited(context.Build.TryRemove(((RemoveLine)command).Position));
        }

        /// <summary>
        /// Moves a line between source positions.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="MoveLine"/> command.</param>
        /// <returns>The edit result.</returns>
        internal HandlerOutcome Move(SessionContext context, ISessionCommand command)
        {
            MoveLine move = (MoveLine)command;
            return Edited(context.Build.TryMove(move.Origin, move.Destination));
        }

        /// <summary>
        /// Exchanges the lines at two source positions.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="SwapLines"/> command.</param>
        /// <returns>The edit result.</returns>
        internal HandlerOutcome Swap(SessionContext context, ISessionCommand command)
        {
            SwapLines swap = (SwapLines)command;
            return Edited(context.Build.TrySwap(swap.First, swap.Second));
        }

        /// <summary>
        /// Archives a Buffer item, or the incoming item when none is named.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="ArchiveItem"/> command.</param>
        /// <returns>The archive result.</returns>
        internal HandlerOutcome Archive(SessionContext context, ISessionCommand command)
        {
            ArchiveItem archive = (ArchiveItem)command;
            ProcessState process = context.Process;
            ArchiveResult archived = archive.Item.HasValue ? process.Archive(archive.Item.Value) : process.ArchiveIncoming();
            return new HandlerOutcome(archived.Succeeded, archived, archived.Succeeded ? ArchiveAreas : ChangedAreas.None);
        }

        /// <summary>
        /// Activates a Directive held in the Buffer.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="ConfirmDirective"/> command.</param>
        /// <returns>The edit result.</returns>
        internal HandlerOutcome Activate(SessionContext context, ISessionCommand command)
        {
            return Edited(context.Build.TryActivateDirective(((ConfirmDirective)command).Directive));
        }

        /// <summary>
        /// Wraps an edit result.
        /// </summary>
        /// <param name="edited">The edit result.</param>
        /// <returns>The outcome.</returns>
        private static HandlerOutcome Edited(BuildOperationResult edited)
        {
            return new HandlerOutcome(edited.Succeeded, edited, edited.Succeeded ? EditAreas : ChangedAreas.None);
        }

        /// <summary>
        /// Whether a source position holds a line a new item would displace.
        /// </summary>
        /// <param name="arrangement">The current arrangement.</param>
        /// <param name="position">The position.</param>
        /// <returns>True when the position holds an Instruction or a Structure.</returns>
        private static bool IsOccupied(SourceArrangement arrangement, SourcePosition position)
        {
            if (position.LineNumber > arrangement.SlotCount)
                return false;

            SourceSlotKind kind = arrangement.SlotAt(position).Kind;
            return kind is SourceSlotKind.Instruction or SourceSlotKind.StructureHeader or SourceSlotKind.ContainedInstruction;
        }
    }
}