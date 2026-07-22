using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Verifies the <see cref="DependencyInstance"/> record: member round-trips, the null-definition
    /// guard, and identity-keyed value equality.
    /// </summary>
    public sealed class DependencyInstanceTests
    {
        [Test]
        public void Construction_RoundTripsMembers()
        {
            InstanceID instanceID = new InstanceID(7);
            DependencyDefinition definition = Definition();

            DependencyInstance instance = new DependencyInstance(instanceID, definition);

            Assert.AreEqual(instanceID, instance.InstanceID);
            Assert.AreSame(definition, instance.Definition);
        }

        [Test]
        public void Construction_NullDefinition_Throws()
        {
            Assert.Throws<ArgumentException>(() => new DependencyInstance(new InstanceID(1), null));
        }

        [Test]
        public void Equality_EqualMembers_AreEqual()
        {
            DependencyDefinition definition = Definition();

            DependencyInstance first = new DependencyInstance(new InstanceID(3), definition);
            DependencyInstance second = new DependencyInstance(new InstanceID(3), definition);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void Equality_DifferingInstanceID_AreUnequal()
        {
            DependencyDefinition definition = Definition();

            DependencyInstance first = new DependencyInstance(new InstanceID(3), definition);
            DependencyInstance second = new DependencyInstance(new InstanceID(4), definition);

            Assert.AreNotEqual(first, second);
        }

        /// <summary>
        /// Builds a minimal effect-less Dependency definition for instance construction.
        /// </summary>
        /// <returns>A frozen test definition.</returns>
        private static DependencyDefinition Definition()
        {
            return new DependencyDefinition(
                new DependencyID("WB-DEP-900"),
                "Test rules.",
                "TEST DEPENDENCY",
                ContentCategory.Dependency,
                Rarity.Starter,
                new List<string>(),
                0,
                new List<EffectDefinition>());
        }
    }
}
