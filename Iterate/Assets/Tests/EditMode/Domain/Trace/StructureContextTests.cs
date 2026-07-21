using System;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for <see cref="StructureContext"/>: a required non-empty Structure ancestry and
    /// entry identity, null-for-not-applicable iteration and Condition identities, and structural
    /// equality over the ancestry.
    /// </summary>
    public sealed class StructureContextTests
    {
        [Test]
        public void Construction_RoundTripsAllFields()
        {
            // Arrange
            InstanceID[] ancestry = { new(1), new(2) };

            // Act
            StructureContext context = new(ancestry, "STRUCT:1", "ITER:3", "COND:4");

            // Assert
            Assert.That(context.StructureAncestry, Is.EqualTo(ancestry));
            Assert.That(context.StructureEntryIdentity, Is.EqualTo("STRUCT:1"));
            Assert.That(context.RepeatIterationIdentity, Is.EqualTo("ITER:3"));
            Assert.That(context.ConditionEvaluationIdentity, Is.EqualTo("COND:4"));
        }

        [Test]
        public void Construction_NullAncestry_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new StructureContext(null, "STRUCT:1", null, null));
        }

        [Test]
        public void Construction_EmptyAncestry_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new StructureContext(Array.Empty<InstanceID>(), "STRUCT:1", null, null));
        }

        [Test]
        public void Construction_EmptyEntryIdentity_Throws()
        {
            InstanceID[] ancestry = { new(1) };

            Assert.Throws<ArgumentException>(() => _ = new StructureContext(ancestry, "", null, null));
        }

        [Test]
        public void Construction_NullIterationAndCondition_Accepted()
        {
            // Arrange
            InstanceID[] ancestry = { new(1) };

            // Act — null iteration and Condition identities are the not-applicable case.
            StructureContext context = new(ancestry, "STRUCT:1", null, null);

            // Assert
            Assert.That(context.RepeatIterationIdentity, Is.Null);
            Assert.That(context.ConditionEvaluationIdentity, Is.Null);
        }

        [Test]
        public void Construction_EmptyIterationIdentity_Throws()
        {
            InstanceID[] ancestry = { new(1) };

            Assert.Throws<ArgumentException>(() => _ = new StructureContext(ancestry, "STRUCT:1", "", null));
        }

        [Test]
        public void Construction_EmptyConditionIdentity_Throws()
        {
            InstanceID[] ancestry = { new(1) };

            Assert.Throws<ArgumentException>(() => _ = new StructureContext(ancestry, "STRUCT:1", null, ""));
        }

        [Test]
        public void Equality_IsStructuralOverAncestry()
        {
            // Arrange — independently built ancestry lists with identical entries.
            InstanceID[] leftAncestry = { new(1), new(2) };
            InstanceID[] rightAncestry = { new(1), new(2) };
            StructureContext left = new(leftAncestry, "STRUCT:1", "ITER:3", null);
            StructureContext right = new(rightAncestry, "STRUCT:1", "ITER:3", null);

            // Act & Assert
            Assert.That(right, Is.EqualTo(left));
            Assert.That(right.GetHashCode(), Is.EqualTo(left.GetHashCode()));
        }
    }
}
