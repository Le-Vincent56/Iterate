using System;
using NUnit.Framework;
using Iterate.Application.SessionFlow;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests <see cref="CommandRegistry"/>: legality is "a route exists" and dispatch is "invoke it",
    /// so the two can never disagree. A duplicate key is a wiring defect and throws at registration
    /// rather than silently shadowing the first route.
    /// </summary>
    public sealed class CommandRegistryTests
    {
        private static readonly SessionStage[] _allStages =
        {
            SessionStage.NoSession,
            SessionStage.RouteSelection,
            SessionStage.Shop,
            SessionStage.BranchConfiguration,
            SessionStage.Build,
            SessionStage.ResultReview,
            SessionStage.Rewards,
            SessionStage.SessionEnded,
        };

        private sealed record UnlistedCommand : ISessionCommand;

        [Test]
        public void Add_DuplicateKey_Throws()
        {
            CommandRegistry registry = new CommandRegistry();
            registry.Add(RouteFor(typeof(Submit), SessionStage.ResultReview));

            Assert.Throws<ArgumentException>(() => registry.Add(RouteFor(typeof(Submit), SessionStage.ResultReview)));
        }

        [Test]
        public void Add_SameCommandInAnotherStage_IsAccepted()
        {
            CommandRegistry registry = new CommandRegistry();
            registry.Add(RouteFor(typeof(InstallPatch), SessionStage.Shop));
            registry.Add(RouteFor(typeof(InstallPatch), SessionStage.Rewards));

            Assert.IsTrue(registry.IsLegal(typeof(InstallPatch), SessionStage.Shop));
            Assert.IsTrue(registry.IsLegal(typeof(InstallPatch), SessionStage.Rewards));
        }

        [Test]
        public void Add_NullRoute_ThrowsArgumentNullException()
        {
            CommandRegistry registry = new CommandRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Add(null));
        }

        [Test]
        public void CommandRoute_NullApply_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new CommandRoute(new CommandRouteKey(typeof(Submit), SessionStage.ResultReview), null)
            );
        }

        [Test]
        public void TryGet_Registered_ReturnsTheSameRoute()
        {
            CommandRegistry registry = new CommandRegistry();
            CommandRoute route = RouteFor(typeof(RerollShop), SessionStage.Shop);
            registry.Add(route);

            bool found = registry.TryGet(typeof(RerollShop), SessionStage.Shop, out CommandRoute resolved);

            Assert.IsTrue(found);
            Assert.AreSame(route, resolved);
        }

        [Test]
        public void TryGet_RegisteredInAnotherStage_MissesCleanly()
        {
            CommandRegistry registry = new CommandRegistry();
            registry.Add(RouteFor(typeof(RerollShop), SessionStage.Shop));

            bool found = registry.TryGet(typeof(RerollShop), SessionStage.Build, out CommandRoute resolved);

            Assert.IsFalse(found);
            Assert.IsNull(resolved);
        }

        [Test]
        public void IsLegal_IsTrueExactlyWhenTryGetSucceeds()
        {
            CommandRegistry registry = new CommandRegistry();
            registry.Add(RouteFor(typeof(SelectRoute), SessionStage.RouteSelection));
            registry.Add(RouteFor(typeof(AbandonSession), SessionStage.Build));
            registry.Add(RouteFor(typeof(ConfigureBranch), SessionStage.BranchConfiguration));

            foreach (Type command in SessionCommandRoutes.Commands)
            {
                foreach (SessionStage stage in _allStages)
                {
                    bool found = registry.TryGet(command, stage, out CommandRoute _);
                    Assert.AreEqual(found, registry.IsLegal(command, stage), $"{command.Name} in {stage}");
                }
            }
        }

        [Test]
        public void IsLegal_OverTheFullTable_MatchesKeys()
        {
            CommandRegistry registry = new CommandRegistry();
            foreach (CommandRouteKey key in SessionCommandRoutes.Keys)
                registry.Add(new CommandRoute(key, AcceptNothing));

            foreach (CommandRouteKey key in SessionCommandRoutes.Keys)
                Assert.IsTrue(registry.IsLegal(key.Command, key.Stage), key.ToString());
        }

        [Test]
        public void IsLegal_UnlistedCommandType_IsIllegalInEveryStage()
        {
            CommandRegistry registry = new CommandRegistry();
            foreach (CommandRouteKey key in SessionCommandRoutes.Keys)
                registry.Add(new CommandRoute(key, AcceptNothing));

            foreach (SessionStage stage in _allStages)
                Assert.IsFalse(registry.IsLegal(typeof(UnlistedCommand), stage), stage.ToString());
        }

        [Test]
        public void IsLegal_NullCommandType_ThrowsArgumentNullException()
        {
            CommandRegistry registry = new CommandRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.IsLegal(null, SessionStage.Build));
        }

        /// <summary>
        /// Builds a route whose handler accepts and changes nothing; these tests never invoke it.
        /// </summary>
        /// <param name="command">The command type the route serves.</param>
        /// <param name="stage">The stage the route serves.</param>
        /// <returns>The route.</returns>
        private static CommandRoute RouteFor(Type command, SessionStage stage)
        {
            return new CommandRoute(new CommandRouteKey(command, stage), AcceptNothing);
        }

        /// <summary>
        /// A handler stand-in that reports acceptance with no changed areas.
        /// </summary>
        /// <param name="context">The Session context; unused.</param>
        /// <param name="command">The command; unused.</param>
        /// <returns>An accepted outcome with no Domain result.</returns>
        private static HandlerOutcome AcceptNothing(SessionContext context, ISessionCommand command)
        {
            return new HandlerOutcome(true, null, ChangedAreas.None);
        }
    }
}
