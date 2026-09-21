using System;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests the monotonic <see cref="InstanceIDSource"/> and its restore seam: allocation exposes how
    /// far the counter has run, and a restored source continues from that point rather than from one, so
    /// a Session rebuilt from a snapshot mints the identities the uninterrupted Session would have.
    /// </summary>
    public sealed class InstanceIDSourceTests
    {
        [Test]
        public void LastAllocated_BeforeAnyAllocation_IsZero()
        {
            InstanceIDSource source = new();

            Assert.AreEqual(0, source.LastAllocated);
        }

        [Test]
        public void Next_AdvancesLastAllocatedInStep()
        {
            InstanceIDSource source = new();

            Assert.AreEqual(1, source.Next().Value);
            Assert.AreEqual(1, source.LastAllocated);
            Assert.AreEqual(2, source.Next().Value);
            Assert.AreEqual(2, source.LastAllocated);
        }

        [Test]
        public void Restore_ContinuesFromTheGivenAllocation()
        {
            InstanceIDSource source = InstanceIDSource.Restore(5);

            Assert.AreEqual(5, source.LastAllocated);
            Assert.AreEqual(new InstanceID(6), source.Next());
            Assert.AreEqual(6, source.LastAllocated);
        }

        [Test]
        public void Restore_Zero_AllocatesFromOneLikeAFreshSource()
        {
            InstanceIDSource source = InstanceIDSource.Restore(0);

            Assert.AreEqual(0, source.LastAllocated);
            Assert.AreEqual(new InstanceID(1), source.Next());
        }

        [Test]
        public void Restore_NegativeAllocation_Throws()
        {
            Assert.Throws<ArgumentException>(() => InstanceIDSource.Restore(-1));
        }

        [Test]
        public void Restore_ProducesAnIndependentSource()
        {
            InstanceIDSource original = new();
            original.Next();
            original.Next();

            InstanceIDSource restored = InstanceIDSource.Restore(original.LastAllocated);
            restored.Next();

            Assert.AreEqual(2, original.LastAllocated, "restoring must not advance the source it was read from.");
            Assert.AreEqual(3, restored.LastAllocated);
        }
    }
}
