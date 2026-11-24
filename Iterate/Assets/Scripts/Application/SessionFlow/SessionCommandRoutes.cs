using System;
using System.Collections.Generic;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The fixed command list and the legality table: one row per (command type, stage) pair in which
    /// the command is legal. No stage outside an active Session admits a command. Finer legality, such
    /// as a below-Pass result refusing Submit, belongs to the handler, not to this table.
    /// </summary>
    public static class SessionCommandRoutes
    {
        private static readonly Type[] _commands =
        {
            typeof(InstallLine),
            typeof(RemoveLine),
            typeof(MoveLine),
            typeof(SwapLines),
            typeof(ArchiveItem),
            typeof(ConfirmDirective),
            typeof(CompileAndExecute),
            typeof(Submit),
            typeof(Continue),
            typeof(ReturnToBuild),
            typeof(PurchaseOffer),
            typeof(RerollShop),
            typeof(PinOffer),
            typeof(InstallPatch),
            typeof(InstallDependency),
            typeof(DestroyDependency),
            typeof(DeleteItem),
            typeof(DuplicateItem),
            typeof(LeaveShop),
            typeof(ConfigureBranch),
            typeof(SelectRoute),
            typeof(SelectReward),
            typeof(AbandonSession),
        };

        private static readonly CommandRouteKey[] _keys =
        {
            new(typeof(SelectRoute), SessionStage.RouteSelection),
            new(typeof(AbandonSession), SessionStage.RouteSelection),

            new(typeof(PurchaseOffer), SessionStage.Shop),
            new(typeof(RerollShop), SessionStage.Shop),
            new(typeof(PinOffer), SessionStage.Shop),
            new(typeof(InstallPatch), SessionStage.Shop),
            new(typeof(InstallDependency), SessionStage.Shop),
            new(typeof(DestroyDependency), SessionStage.Shop),
            new(typeof(DeleteItem), SessionStage.Shop),
            new(typeof(DuplicateItem), SessionStage.Shop),
            new(typeof(LeaveShop), SessionStage.Shop),
            new(typeof(AbandonSession), SessionStage.Shop),

            new(typeof(ConfigureBranch), SessionStage.BranchConfiguration),
            new(typeof(AbandonSession), SessionStage.BranchConfiguration),

            new(typeof(InstallLine), SessionStage.Build),
            new(typeof(RemoveLine), SessionStage.Build),
            new(typeof(MoveLine), SessionStage.Build),
            new(typeof(SwapLines), SessionStage.Build),
            new(typeof(ArchiveItem), SessionStage.Build),
            new(typeof(ConfirmDirective), SessionStage.Build),
            new(typeof(CompileAndExecute), SessionStage.Build),
            new(typeof(AbandonSession), SessionStage.Build),

            new(typeof(Submit), SessionStage.ResultReview),
            new(typeof(Continue), SessionStage.ResultReview),
            new(typeof(ReturnToBuild), SessionStage.ResultReview),
            new(typeof(AbandonSession), SessionStage.ResultReview),

            new(typeof(SelectReward), SessionStage.Rewards),
            new(typeof(InstallPatch), SessionStage.Rewards),
            new(typeof(AbandonSession), SessionStage.Rewards),
        };

        /// <summary>
        /// Every command type, in the order the command seam lists them.
        /// </summary>
        public static IReadOnlyList<Type> Commands { get; } = Array.AsReadOnly(_commands);

        /// <summary>
        /// The legality table, one row per legal (command type, stage) pair, grouped by stage.
        /// </summary>
        public static IReadOnlyList<CommandRouteKey> Keys { get; } = Array.AsReadOnly(_keys);

        /// <summary>
        /// Verifies that a registry binds exactly the legality table: a route for every key and no
        /// route without one.
        /// </summary>
        /// <param name="registry">The registry to verify.</param>
        /// <returns>The registry unchanged.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the registry is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the registry and the table differ.</exception>
        public static CommandRegistry RequireMatches(CommandRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            for (int index = 0; index < _keys.Length; index++)
            {
                if (!registry.IsLegal(_keys[index].Command, _keys[index].Stage))
                    throw new InvalidOperationException($"No route is bound for {_keys[index].Command.Name} in {_keys[index].Stage}.");
            }

            if (registry.Count != _keys.Length)
                throw new InvalidOperationException($"The registry binds {registry.Count} routes; the legality table has {_keys.Length}.");

            return registry;
        }

        /// <summary>
        /// Binds every row of the legality table to the handler method that applies it, then verifies
        /// the result against the table.
        /// </summary>
        /// <param name="build">The Build-stage handler.</param>
        /// <param name="branch">The Branch-configuration handler.</param>
        /// <param name="shop">The shop handler.</param>
        /// <param name="rewards">The rewards handler.</param>
        /// <param name="flow">The flow handler.</param>
        /// <returns>The verified registry.</returns>
        internal static CommandRegistry Build(
            BuildCommandHandler build,
            BranchCommandHandler branch,
            ShopCommandHandler shop,
            RewardCommandHandler rewards,
            FlowCommandHandler flow
        )
        {
            CommandRegistry registry = new();

            registry.Add(Route(typeof(SelectRoute), SessionStage.RouteSelection, flow.ChooseRoute));
            registry.Add(Route(typeof(AbandonSession), SessionStage.RouteSelection, flow.Abandon));

            registry.Add(Route(typeof(PurchaseOffer), SessionStage.Shop, shop.Purchase));
            registry.Add(Route(typeof(RerollShop), SessionStage.Shop, shop.Reroll));
            registry.Add(Route(typeof(PinOffer), SessionStage.Shop, shop.Pin));
            registry.Add(Route(typeof(InstallPatch), SessionStage.Shop, shop.AttachPatch));
            registry.Add(Route(typeof(InstallDependency), SessionStage.Shop, shop.BuyDependency));
            registry.Add(Route(typeof(DestroyDependency), SessionStage.Shop, shop.Destroy));
            registry.Add(Route(typeof(DeleteItem), SessionStage.Shop, shop.Delete));
            registry.Add(Route(typeof(DuplicateItem), SessionStage.Shop, shop.Duplicate));
            registry.Add(Route(typeof(LeaveShop), SessionStage.Shop, shop.Leave));
            registry.Add(Route(typeof(AbandonSession), SessionStage.Shop, flow.Abandon));

            registry.Add(Route(typeof(ConfigureBranch), SessionStage.BranchConfiguration, branch.Configure));
            registry.Add(Route(typeof(AbandonSession), SessionStage.BranchConfiguration, flow.Abandon));

            registry.Add(Route(typeof(InstallLine), SessionStage.Build, build.Install));
            registry.Add(Route(typeof(RemoveLine), SessionStage.Build, build.Remove));
            registry.Add(Route(typeof(MoveLine), SessionStage.Build, build.Move));
            registry.Add(Route(typeof(SwapLines), SessionStage.Build, build.Swap));
            registry.Add(Route(typeof(ArchiveItem), SessionStage.Build, build.Archive));
            registry.Add(Route(typeof(ConfirmDirective), SessionStage.Build, build.Activate));
            registry.Add(Route(typeof(CompileAndExecute), SessionStage.Build, flow.CompileAndRun));
            registry.Add(Route(typeof(AbandonSession), SessionStage.Build, flow.Abandon));

            registry.Add(Route(typeof(Submit), SessionStage.ResultReview, flow.SubmitReviewed));
            registry.Add(Route(typeof(Continue), SessionStage.ResultReview, flow.Discard));
            registry.Add(Route(typeof(ReturnToBuild), SessionStage.ResultReview, flow.LeaveReview));
            registry.Add(Route(typeof(AbandonSession), SessionStage.ResultReview, flow.Abandon));

            registry.Add(Route(typeof(SelectReward), SessionStage.Rewards, rewards.Select));
            registry.Add(Route(typeof(InstallPatch), SessionStage.Rewards, rewards.AttachGrant));
            registry.Add(Route(typeof(AbandonSession), SessionStage.Rewards, flow.Abandon));

            return RequireMatches(registry);
        }

        /// <summary>
        /// Builds one route.
        /// </summary>
        /// <param name="command">The command type.</param>
        /// <param name="stage">The stage.</param>
        /// <param name="apply">The handler method.</param>
        /// <returns>The route.</returns>
        private static CommandRoute Route(Type command, SessionStage stage, Func<SessionContext, ISessionCommand, HandlerOutcome> apply)
        {
            return new CommandRoute(new CommandRouteKey(command, stage), apply);
        }
    }
}