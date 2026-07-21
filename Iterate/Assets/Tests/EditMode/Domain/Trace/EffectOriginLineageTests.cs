using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for <see cref="EffectOriginLineage"/>: the root-first, parent-copied,
    /// creator-appended origin chain with a containment test and structural equality.
    /// </summary>
    public sealed class EffectOriginLineageTests
    {
        [Test]
        public void Empty_HasNoEntries()
        {
            // Arrange & Act
            EffectOriginLineage empty = EffectOriginLineage.Empty;

            // Assert
            Assert.That(empty.Depth, Is.EqualTo(0));
            Assert.That(empty.Entries, Is.Empty);
            Assert.That(empty.Contains(new InstanceID(1)), Is.False);
        }

        [Test]
        public void Append_BuildsRootFirstChain()
        {
            // Arrange
            InstanceID root = new(1);
            InstanceID child = new(2);

            // Act
            EffectOriginLineage lineage = EffectOriginLineage.Empty.Append(root).Append(child);

            // Assert
            Assert.That(lineage.Depth, Is.EqualTo(2));
            Assert.That(lineage.Entries, Is.EqualTo(new[] { root, child }));
        }

        [Test]
        public void Append_LeavesParentUnchanged()
        {
            // Arrange
            EffectOriginLineage parent = EffectOriginLineage.Empty.Append(new InstanceID(1));

            // Act
            EffectOriginLineage child = parent.Append(new InstanceID(2));

            // Assert — the parent value is untouched by the append.
            Assert.That(parent.Depth, Is.EqualTo(1));
            Assert.That(parent.Entries, Is.EqualTo(new[] { new InstanceID(1) }));
            Assert.That(child.Depth, Is.EqualTo(2));
        }

        [Test]
        public void Contains_AnswersTheOriginLockTest()
        {
            // Arrange
            EffectOriginLineage lineage = EffectOriginLineage.Empty.Append(new InstanceID(1)).Append(new InstanceID(2));

            // Act & Assert
            Assert.That(lineage.Contains(new InstanceID(1)), Is.True);
            Assert.That(lineage.Contains(new InstanceID(2)), Is.True);
            Assert.That(lineage.Contains(new InstanceID(3)), Is.False);
        }

        [Test]
        public void Equality_IsStructural()
        {
            // Arrange — two independently built chains with identical entries.
            EffectOriginLineage left = EffectOriginLineage.Empty.Append(new InstanceID(1)).Append(new InstanceID(2));
            EffectOriginLineage right = EffectOriginLineage.Empty.Append(new InstanceID(1)).Append(new InstanceID(2));

            // Act & Assert
            Assert.That(right, Is.EqualTo(left));
            Assert.That(right.GetHashCode(), Is.EqualTo(left.GetHashCode()));
        }

        [Test]
        public void Equality_DiffersByOrder()
        {
            // Arrange
            EffectOriginLineage forward = EffectOriginLineage.Empty.Append(new InstanceID(1)).Append(new InstanceID(2));
            EffectOriginLineage reversed = EffectOriginLineage.Empty.Append(new InstanceID(2)).Append(new InstanceID(1));

            // Act & Assert
            Assert.That(reversed, Is.Not.EqualTo(forward));
        }
    }
}
