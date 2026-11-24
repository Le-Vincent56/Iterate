using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Iterate.Application.SessionFlow;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests <see cref="SessionCommandRoutes"/>: the fixed command list and the (command type, stage)
    /// legality table that is the single statement of which command is legal where.
    ///
    /// The expected table below is written out literally rather than derived from the production
    /// table, so a row added or dropped by mistake fails here instead of agreeing with itself. The
    /// sweep covers every command against every stage, so an illegal pair is asserted as firmly as a
    /// legal one.
    /// </summary>
    public sealed class SessionCommandRoutesTests
    {
        private static readonly Type[] _expectedCommandOrder =
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

        [Test]
        public void Commands_ListsTheTwentySeamCommandsInSeamOrder()
        {
            CollectionAssert.AreEqual(_expectedCommandOrder, ToArray(SessionCommandRoutes.Commands));
        }

        [Test]
        public void Commands_ContainsEveryConcreteSessionCommandInTheAssemblyExactlyOnce()
        {
            Type marker = typeof(ISessionCommand);
            List<Type> concrete = new List<Type>();
            foreach (Type type in marker.Assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || !marker.IsAssignableFrom(type))
                    continue;

                concrete.Add(type);
            }

            Assert.AreEqual(23, concrete.Count, "The vertical slice declares exactly twenty-three commands.");
            Assert.AreEqual(concrete.Count, SessionCommandRoutes.Commands.Count);
            foreach (Type type in concrete)
                Assert.AreEqual(1, CountOf(SessionCommandRoutes.Commands, type), type.Name);
        }

        [Test]
        public void Keys_EqualTheExpectedTableAcrossEveryCommandAndStage()
        {
            HashSet<CommandRouteKey> expected = ExpectedKeys();
            HashSet<CommandRouteKey> actual = new HashSet<CommandRouteKey>();
            foreach (CommandRouteKey key in SessionCommandRoutes.Keys)
                actual.Add(key);

            List<string> mismatches = new List<string>();
            foreach (Type command in _expectedCommandOrder)
            {
                foreach (SessionStage stage in _allStages)
                {
                    CommandRouteKey key = new CommandRouteKey(command, stage);
                    bool shouldBeLegal = expected.Contains(key);
                    if (shouldBeLegal != actual.Contains(key))
                        mismatches.Add($"{command.Name} in {stage}{(shouldBeLegal ? " is missing" : " is unexpected")}");
                }
            }

            CollectionAssert.IsEmpty(mismatches, string.Join("; ", mismatches));
        }

        [Test]
        public void Keys_HaveOneRowPerLegalPairAndNoDuplicates()
        {
            HashSet<CommandRouteKey> distinct = new HashSet<CommandRouteKey>();
            foreach (CommandRouteKey key in SessionCommandRoutes.Keys)
                Assert.IsTrue(distinct.Add(key), $"Duplicate row: {key}");

            Assert.AreEqual(ExpectedKeys().Count, SessionCommandRoutes.Keys.Count);
        }

        [Test]
        public void Keys_NameOnlyListedCommands()
        {
            foreach (CommandRouteKey key in SessionCommandRoutes.Keys)
                Assert.AreEqual(1, CountOf(SessionCommandRoutes.Commands, key.Command), key.Command.Name);
        }

        [TestCase(SessionStage.NoSession)]
        [TestCase(SessionStage.SessionEnded)]
        public void Keys_OutsideAnActiveSession_AdmitNoCommand(SessionStage stage)
        {
            Assert.AreEqual(0, CountRowsIn(stage));
        }

        [Test]
        public void Keys_ShopRow_CarriesLeaveShopAsItsExit()
        {
            // The Shop row holds the six shop operations, the two item Services, the exit and
            // AbandonSession. LeaveShop is the exit's canonical command; a stage-scoped Continue would serve two intents.
            Assert.AreEqual(10, CountRowsIn(SessionStage.Shop));
        }

        /// <summary>
        /// The legality table, written out literally per stage.
        /// </summary>
        /// <returns>Every legal (command type, stage) pair.</returns>
        private static HashSet<CommandRouteKey> ExpectedKeys()
        {
            HashSet<CommandRouteKey> keys = new HashSet<CommandRouteKey>();

            AddRow(keys, SessionStage.RouteSelection, typeof(SelectRoute), typeof(AbandonSession));
            AddRow(
                keys,
                SessionStage.Shop,
                typeof(PurchaseOffer),
                typeof(RerollShop),
                typeof(PinOffer),
                typeof(InstallPatch),
                typeof(InstallDependency),
                typeof(DestroyDependency),
                typeof(DeleteItem),
                typeof(DuplicateItem),
                typeof(LeaveShop),
                typeof(AbandonSession)
            );
            AddRow(keys, SessionStage.BranchConfiguration, typeof(ConfigureBranch), typeof(AbandonSession));
            AddRow(
                keys,
                SessionStage.Build,
                typeof(InstallLine),
                typeof(RemoveLine),
                typeof(MoveLine),
                typeof(SwapLines),
                typeof(ArchiveItem),
                typeof(ConfirmDirective),
                typeof(CompileAndExecute),
                typeof(AbandonSession)
            );
            AddRow(
                keys,
                SessionStage.ResultReview,
                typeof(Submit),
                typeof(Continue),
                typeof(ReturnToBuild),
                typeof(AbandonSession)
            );
            AddRow(keys, SessionStage.Rewards, typeof(SelectReward), typeof(InstallPatch), typeof(AbandonSession));

            return keys;
        }

        /// <summary>
        /// Adds one stage's legal commands to the expected table.
        /// </summary>
        /// <param name="keys">The table being built.</param>
        /// <param name="stage">The stage the row describes.</param>
        /// <param name="commands">The commands legal in that stage.</param>
        private static void AddRow(HashSet<CommandRouteKey> keys, SessionStage stage, params Type[] commands)
        {
            foreach (Type command in commands)
                keys.Add(new CommandRouteKey(command, stage));
        }

        /// <summary>
        /// Counts the production table's rows for one stage.
        /// </summary>
        /// <param name="stage">The stage to count.</param>
        /// <returns>The number of legal commands in that stage.</returns>
        private static int CountRowsIn(SessionStage stage)
        {
            int count = 0;
            foreach (CommandRouteKey key in SessionCommandRoutes.Keys)
            {
                if (key.Stage == stage)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Counts how often a type appears in a list.
        /// </summary>
        /// <param name="types">The list to search.</param>
        /// <param name="type">The type to count.</param>
        /// <returns>The number of occurrences.</returns>
        private static int CountOf(IReadOnlyList<Type> types, Type type)
        {
            int count = 0;
            foreach (Type candidate in types)
            {
                if (candidate == type)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Copies a read-only list into an array for collection assertions.
        /// </summary>
        /// <param name="types">The list to copy.</param>
        /// <returns>The copied array.</returns>
        private static Type[] ToArray(IReadOnlyList<Type> types)
        {
            Type[] copy = new Type[types.Count];
            for (int index = 0; index < types.Count; index++)
                copy[index] = types[index];

            return copy;
        }
    }
}
