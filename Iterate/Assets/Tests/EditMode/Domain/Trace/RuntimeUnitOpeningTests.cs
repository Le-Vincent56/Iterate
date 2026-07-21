using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for <see cref="RuntimeUnitOpening"/>: exactly one host identity per ownership, and
    /// the activation-consistency rules — an added execution carries a request identity, a depth of at
    /// least one, and a parent unit, while a canonical traversal carries none of the three.
    /// </summary>
    public sealed class RuntimeUnitOpeningTests
    {
        [Test]
        public void CoreOwnedCanonical_Constructs()
        {
            // Act
            RuntimeUnitOpening opening = Build();

            // Assert
            Assert.That(opening.Ownership, Is.EqualTo(OwnershipClassification.CoreOwned));
            Assert.That(opening.CoreLineIdentity, Is.EqualTo("CORE:1"));
            Assert.That(opening.HostInstance, Is.Null);
            Assert.That(opening.Activation, Is.EqualTo(ActivationKind.CanonicalTraversal));
            Assert.That(opening.AddedExecutionDepth, Is.EqualTo(0));
            Assert.That(opening.Lineage, Is.EqualTo(EffectOriginLineage.Empty));
        }

        [Test]
        public void PlayerOwnedWithoutHostInstance_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(
                ownership: OwnershipClassification.PlayerOwned,
                coreLineIdentity: null));
        }

        [Test]
        public void PlayerOwnedWithCoreLineIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(
                ownership: OwnershipClassification.PlayerOwned,
                hostInstance: new InstanceID(1),
                coreLineIdentity: "CORE:1"));
        }

        [Test]
        public void CoreOwnedWithHostInstance_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(
                ownership: OwnershipClassification.CoreOwned,
                hostInstance: new InstanceID(1)));
        }

        [Test]
        public void AddedActivationWithoutRequestIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(
                activation: ActivationKind.AddedExecution,
                addedExecutionDepth: 1,
                parentUnit: new RuntimeUnitID(1)));
        }

        [Test]
        public void AddedActivationWithoutParentUnit_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(
                activation: ActivationKind.AddedExecution,
                originatingRequestIdentity: "REQ:1",
                addedExecutionDepth: 1));
        }

        [Test]
        public void AddedActivationWithZeroDepth_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(
                activation: ActivationKind.AddedExecution,
                originatingRequestIdentity: "REQ:1",
                addedExecutionDepth: 0,
                parentUnit: new RuntimeUnitID(1)));
        }

        [Test]
        public void CanonicalActivationWithRequestIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(originatingRequestIdentity: "REQ:1"));
        }

        [Test]
        public void CanonicalActivationWithDepth_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(addedExecutionDepth: 1));
        }

        [Test]
        public void CanonicalActivationWithParentUnit_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(parentUnit: new RuntimeUnitID(1)));
        }

        [Test]
        public void ValidAddedOpening_RoundTrips()
        {
            // Arrange
            EffectOriginLineage lineage = EffectOriginLineage.Empty.Append(new InstanceID(9));

            // Act
            RuntimeUnitOpening opening = Build(
                activation: ActivationKind.AddedExecution,
                originatingRequestIdentity: "REQ:1",
                addedExecutionDepth: 1,
                parentUnit: new RuntimeUnitID(1),
                lineage: lineage);

            // Assert
            Assert.That(opening.Activation, Is.EqualTo(ActivationKind.AddedExecution));
            Assert.That(opening.OriginatingRequestIdentity, Is.EqualTo("REQ:1"));
            Assert.That(opening.AddedExecutionDepth, Is.EqualTo(1));
            Assert.That(opening.ParentUnit, Is.EqualTo(new RuntimeUnitID(1)));
            Assert.That(opening.Lineage, Is.EqualTo(lineage));
        }

        private static RuntimeUnitOpening Build(
            InstanceID? hostInstance = null,
            string coreLineIdentity = "CORE:1",
            OwnershipClassification ownership = OwnershipClassification.CoreOwned,
            SourcePosition? position = null,
            ActivationKind activation = ActivationKind.CanonicalTraversal,
            string originatingRequestIdentity = null,
            int addedExecutionDepth = 0,
            EffectOriginLineage lineage = null,
            StructureContext structureContext = null,
            RuntimeUnitID? parentUnit = null
        )
        {
            return new RuntimeUnitOpening(
                hostInstance,
                coreLineIdentity,
                ownership,
                position,
                activation,
                originatingRequestIdentity,
                addedExecutionDepth,
                lineage ?? EffectOriginLineage.Empty,
                structureContext,
                parentUnit);
        }
    }
}
