using System;
using NUnit.Framework;
using Iterate.Application.SessionFlow;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests <see cref="SessionCommandRoutes.RequireMatches"/>, the check the route binding passes
    /// through at construction: a registry must bind a route for every row of the legality table and
    /// no route the table lacks, so the table and the handlers can never drift apart silently.
    /// </summary>
    public sealed class SessionCommandRoutesBindingTests
    {
        [Test]
        public void RequireMatches_ExactlyTheTable_ReturnsTheRegistry()
        {
            CommandRegistry registry = FullTable();

            Assert.AreSame(registry, SessionCommandRoutes.RequireMatches(registry));
        }

        [Test]
        public void RequireMatches_AKeyWithNoRoute_Throws()
        {
            CommandRegistry registry = new();
            for (int index = 1; index < SessionCommandRoutes.Keys.Count; index++)
                registry.Add(new CommandRoute(SessionCommandRoutes.Keys[index], AcceptNothing));

            Assert.Throws<InvalidOperationException>(() => SessionCommandRoutes.RequireMatches(registry));
        }

        [Test]
        public void RequireMatches_ARouteWithNoKey_Throws()
        {
            CommandRegistry registry = FullTable();
            registry.Add(new CommandRoute(new CommandRouteKey(typeof(SelectRoute), SessionStage.Build), AcceptNothing));

            Assert.Throws<InvalidOperationException>(() => SessionCommandRoutes.RequireMatches(registry));
        }

        [Test]
        public void RequireMatches_NullRegistry_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SessionCommandRoutes.RequireMatches(null));
        }

        [Test]
        public void Count_IsTheNumberOfRoutesAdded()
        {
            Assert.AreEqual(SessionCommandRoutes.Keys.Count, FullTable().Count);
            Assert.AreEqual(0, new CommandRegistry().Count);
        }

        /// <summary>
        /// A registry binding every row of the table to a handler stand-in.
        /// </summary>
        /// <returns>The registry.</returns>
        private static CommandRegistry FullTable()
        {
            CommandRegistry registry = new();
            foreach (CommandRouteKey key in SessionCommandRoutes.Keys)
                registry.Add(new CommandRoute(key, AcceptNothing));

            return registry;
        }

        /// <summary>
        /// A handler stand-in that accepts and changes nothing; these tests never invoke it.
        /// </summary>
        /// <param name="context">The context; unused.</param>
        /// <param name="command">The command; unused.</param>
        /// <returns>An accepted outcome.</returns>
        private static HandlerOutcome AcceptNothing(SessionContext context, ISessionCommand command)
        {
            return new HandlerOutcome(true, null, ChangedAreas.None);
        }
    }
}
