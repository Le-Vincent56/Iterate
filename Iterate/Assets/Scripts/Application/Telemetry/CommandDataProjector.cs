using System;
using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Projects each of the TA-SEAM-001 commands into its telemetry target and data, through a
    /// table built once from command type to projection, with no switch on type and no reflection
    /// fallback. A command with no projection is a defect the recorder's fault barrier contains.
    /// </summary>
    public sealed class CommandDataProjector
    {
        private readonly Dictionary<Type, Func<CommandObservation, ResourceChange, CommandProjection>> _projections;

        /// <summary>
        /// Builds the projection table.
        /// </summary>
        public CommandDataProjector()
        {
            _projections = new Dictionary<Type, Func<CommandObservation, ResourceChange, CommandProjection>>
            {
                { typeof(InstallLine), InstallLineOf },
                { typeof(RemoveLine), RemoveLineOf },
                { typeof(MoveLine), MoveLineOf },
                { typeof(SwapLines), SwapLinesOf },
                { typeof(ArchiveItem), ArchiveItemOf },
                { typeof(ConfirmDirective), ConfirmDirectiveOf },
                { typeof(CompileAndExecute), NothingOf },
                { typeof(Submit), NothingOf },
                { typeof(Continue), NothingOf },
                { typeof(ReturnToBuild), NothingOf },
                { typeof(PurchaseOffer), PurchaseOfferOf },
                { typeof(RerollShop), TokensOf },
                { typeof(PinOffer), PinOfferOf },
                { typeof(InstallPatch), InstallPatchOf },
                { typeof(InstallDependency), InstallDependencyOf },
                { typeof(DestroyDependency), DestroyDependencyOf },
                { typeof(DeleteItem), DeleteItemOf },
                { typeof(DuplicateItem), DuplicateItemOf },
                { typeof(LeaveShop), NothingOf },
                { typeof(ConfigureBranch), ConfigureBranchOf },
                { typeof(SelectRoute), SelectRouteOf },
                { typeof(SelectReward), SelectRewardOf },
                { typeof(AbandonSession), NothingOf },
            };
        }

        /// <summary>
        /// Whether a command type has a projection.
        /// </summary>
        /// <param name="commandType">The command type.</param>
        /// <returns>Whether it is projected.</returns>
        public bool Projects(Type commandType)
        {
            return commandType != null && _projections.ContainsKey(commandType);
        }

        /// <summary>
        /// Projects one observed command.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">The resources before and after it.</param>
        /// <returns>Its target and data.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the command type has no projection.</exception>
        public CommandProjection Project(CommandObservation observed, ResourceChange resources)
        {
            Type type = observed.Command.GetType();
            if (!_projections.TryGetValue(type, out Func<CommandObservation, ResourceChange, CommandProjection> projection))
                throw new InvalidOperationException($"The command {type.Name} has no telemetry projection.");

            return projection(observed, resources);
        }

        /// <summary>
        /// Projects a command with no arguments.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">Unused.</param>
        /// <returns>No target and empty data.</returns>
        private static CommandProjection NothingOf(CommandObservation observed, ResourceChange resources)
        {
            return new CommandProjection(null, new JsonObjectBuilder().Build());
        }

        /// <summary>
        /// Projects an install: the item and the line.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">Unused.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection InstallLineOf(CommandObservation observed, ResourceChange resources)
        {
            InstallLine command = (InstallLine)observed.Command;
            JsonObject data = new JsonObjectBuilder()
                .Add("item", command.Item.Value)
                .Add("line", command.Position.LineNumber)
                .Build();
            return new CommandProjection(command.Item.ToString(), data);
        }

        /// <summary>
        /// Projects a removal: the line.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">Unused.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection RemoveLineOf(CommandObservation observed, ResourceChange resources)
        {
            RemoveLine command = (RemoveLine)observed.Command;
            return new CommandProjection(LineTarget(command.Position.LineNumber), new JsonObjectBuilder().Add("line", command.Position.LineNumber).Build());
        }

        /// <summary>
        /// Projects a move: its origin and destination lines.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">Unused.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection MoveLineOf(CommandObservation observed, ResourceChange resources)
        {
            MoveLine command = (MoveLine)observed.Command;
            JsonObject data = new JsonObjectBuilder()
                .Add("origin", command.Origin.LineNumber)
                .Add("destination", command.Destination.LineNumber)
                .Build();
            return new CommandProjection(LineTarget(command.Origin.LineNumber), data);
        }

        /// <summary>
        /// Projects a swap: its two lines.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">Unused.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection SwapLinesOf(CommandObservation observed, ResourceChange resources)
        {
            SwapLines command = (SwapLines)observed.Command;
            JsonObject data = new JsonObjectBuilder()
                .Add("first", command.First.LineNumber)
                .Add("second", command.Second.LineNumber)
                .Build();
            return new CommandProjection(LineTarget(command.First.LineNumber), data);
        }

        /// <summary>
        /// Projects an archive: the item and its origin, the Buffer or the incoming item.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">Unused.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection ArchiveItemOf(CommandObservation observed, ResourceChange resources)
        {
            ArchiveItem command = (ArchiveItem)observed.Command;
            JsonObject data = new JsonObjectBuilder()
                .Add("item", Instance(command.Item))
                .Add("origin", command.Item.HasValue ? "buffer" : "incoming")
                .Build();
            return new CommandProjection(command.Item?.ToString() ?? "incoming", data);
        }

        /// <summary>
        /// Projects a Directive activation: the Directive.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">Unused.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection ConfirmDirectiveOf(CommandObservation observed, ResourceChange resources)
        {
            ConfirmDirective command = (ConfirmDirective)observed.Command;
            return new CommandProjection(command.Directive.ToString(), new JsonObjectBuilder().Add("directive", command.Directive.Value).Build());
        }

        /// <summary>
        /// Projects a purchase: the slot, the definition acquired, the cost, and Tokens before and after.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">The resources before and after.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection PurchaseOfferOf(CommandObservation observed, ResourceChange resources)
        {
            PurchaseOffer command = (PurchaseOffer)observed.Command;
            PurchaseResult bought = observed.Result.DomainResult as PurchaseResult;
            JsonObjectBuilder builder = new JsonObjectBuilder()
                .Add("slot", command.Slot)
                .Add("offer", bought?.Acquired?.Item.DefinitionID);
            AddTokens(builder, resources);
            return new CommandProjection(SlotTarget(command.Slot), builder.Build());
        }

        /// <summary>
        /// Projects a command whose only data is its Token cost: Tokens before and after.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">The resources before and after.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection TokensOf(CommandObservation observed, ResourceChange resources)
        {
            JsonObjectBuilder builder = new();
            AddTokens(builder, resources);
            return new CommandProjection(null, builder.Build());
        }

        /// <summary>
        /// Projects a pin: the slot and whether it is pinned.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">Unused.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection PinOfferOf(CommandObservation observed, ResourceChange resources)
        {
            PinOffer command = (PinOffer)observed.Command;
            JsonObject data = new JsonObjectBuilder()
                .Add("slot", command.Slot)
                .Add("pinned", command.Pinned)
                .Build();
            return new CommandProjection(SlotTarget(command.Slot), data);
        }

        /// <summary>
        /// Projects a Patch installation: the slot or grant, the host and the socket.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">The resources before and after.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection InstallPatchOf(CommandObservation observed, ResourceChange resources)
        {
            InstallPatch command = (InstallPatch)observed.Command;
            JsonObjectBuilder builder = new JsonObjectBuilder()
                .Add("slot", command.Slot.HasValue ? JsonValues.Integer(command.Slot.Value) : JsonValues.Null())
                .Add("grant", Instance(command.Grant))
                .Add("host", command.Host.Value)
                .Add("socket", command.Socket);
            AddTokens(builder, resources);
            return new CommandProjection(command.Host.ToString(), builder.Build());
        }

        /// <summary>
        /// Projects a Dependency installation: the slot, the Dependencies destroyed first, and RAM and
        /// Tokens before and after.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">The resources before and after.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection InstallDependencyOf(CommandObservation observed, ResourceChange resources)
        {
            InstallDependency command = (InstallDependency)observed.Command;
            JsonObjectBuilder builder = new JsonObjectBuilder()
                .Add("slot", command.Slot)
                .Add("destroyFirst", Instances(command.DestroyFirst));
            builder.Add("ramBefore", resources.Before?.RAMUsed ?? 0);
            builder.Add("ramAfter", resources.After?.RAMUsed ?? 0);
            AddTokens(builder, resources);
            return new CommandProjection(SlotTarget(command.Slot), builder.Build());
        }

        /// <summary>
        /// Projects a Dependency destruction: the Dependency and its refund.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">The resources before and after.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection DestroyDependencyOf(CommandObservation observed, ResourceChange resources)
        {
            DestroyDependency command = (DestroyDependency)observed.Command;
            JsonObjectBuilder builder = new JsonObjectBuilder()
                .Add("dependency", command.Dependency.Value)
                .Add("refund", (resources.After?.Tokens ?? 0) - (resources.Before?.Tokens ?? 0));
            AddTokens(builder, resources);
            return new CommandProjection(command.Dependency.ToString(), builder.Build());
        }

        /// <summary>
        /// Projects an item deletion: the item, and Tokens before and after.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">The resources before and after.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection DeleteItemOf(CommandObservation observed, ResourceChange resources)
        {
            DeleteItem command = (DeleteItem)observed.Command;
            JsonObjectBuilder builder = new JsonObjectBuilder().Add("item", command.Item.Value);
            AddTokens(builder, resources);
            return new CommandProjection(command.Item.ToString(), builder.Build());
        }

        /// <summary>
        /// Projects an item duplication: the item, and Tokens before and after.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">The resources before and after.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection DuplicateItemOf(CommandObservation observed, ResourceChange resources)
        {
            DuplicateItem command = (DuplicateItem)observed.Command;
            JsonObjectBuilder builder = new JsonObjectBuilder().Add("item", command.Item.Value);
            AddTokens(builder, resources);
            return new CommandProjection(command.Item.ToString(), builder.Build());
        }

        /// <summary>
        /// Projects an Active Branch edit: the action and the item.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">Unused.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection ConfigureBranchOf(CommandObservation observed, ResourceChange resources)
        {
            ConfigureBranch command = (ConfigureBranch)observed.Command;
            JsonObject data = new JsonObjectBuilder()
                .Add("action", command.Action.ToString())
                .Add("item", Instance(command.Item))
                .Build();
            return new CommandProjection(command.Item?.ToString(), data);
        }

        /// <summary>
        /// Projects a route choice: the route.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">Unused.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection SelectRouteOf(CommandObservation observed, ResourceChange resources)
        {
            SelectRoute command = (SelectRoute)observed.Command;
            return new CommandProjection(command.Route.Value, new JsonObjectBuilder().Add("route", command.Route.Value).Build());
        }

        /// <summary>
        /// Projects a reward choice: the component, the content chosen, and the items destroyed first.
        /// </summary>
        /// <param name="observed">The command and its result.</param>
        /// <param name="resources">The resources before and after.</param>
        /// <returns>The projection.</returns>
        private static CommandProjection SelectRewardOf(CommandObservation observed, ResourceChange resources)
        {
            SelectReward command = (SelectReward)observed.Command;
            JsonObjectBuilder builder = new JsonObjectBuilder()
                .Add("component", command.ComponentIndex)
                .Add("content", command.ContentID)
                .Add("destroyFirst", Instances(command.DestroyFirst));
            AddTokens(builder, resources);
            return new CommandProjection(command.ContentID, builder.Build());
        }

        /// <summary>
        /// Adds Tokens before and after.
        /// </summary>
        /// <param name="builder">The data builder.</param>
        /// <param name="resources">The resources before and after.</param>
        private static void AddTokens(JsonObjectBuilder builder, ResourceChange resources)
        {
            builder.Add("tokensBefore", resources.Before?.Tokens ?? 0);
            builder.Add("tokensAfter", resources.After?.Tokens ?? 0);
        }

        /// <summary>
        /// An optional instance as a JSON value.
        /// </summary>
        /// <param name="instance">The instance, or null.</param>
        /// <returns>Its number, or JSON null.</returns>
        private static JsonValue Instance(InstanceID? instance)
        {
            return instance.HasValue ? JsonValues.Integer(instance.Value.Value) : JsonValues.Null();
        }

        /// <summary>
        /// A list of instances as a JSON array of numbers.
        /// </summary>
        /// <param name="instances">The instances.</param>
        /// <returns>The array.</returns>
        private static JsonValue Instances(IReadOnlyList<InstanceID> instances)
        {
            List<JsonValue> items = new(instances.Count);
            for (int index = 0; index < instances.Count; index++)
                items.Add(JsonValues.Integer(instances[index].Value));

            return JsonValues.Array(items);
        }

        /// <summary>
        /// A source line as a target.
        /// </summary>
        /// <param name="line">The one-based line.</param>
        /// <returns>The target text.</returns>
        private static string LineTarget(int line)
        {
            return $"line {line}";
        }

        /// <summary>
        /// A shop slot as a target.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <returns>The target text.</returns>
        private static string SlotTarget(int slot)
        {
            return $"slot {slot}";
        }
    }
}