using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for <see cref="EventEvidence"/>: the validated §17.4 common core — family-registry
    /// membership, non-empty subtype and qualifier entries, the null-means-not-applicable rule for every
    /// nullable string, the ownership/host, disposition-reason, and added-execution consistency rules,
    /// and structural equality over the qualifier list.
    /// </summary>
    public sealed class EventEvidenceTests
    {
        [Test]
        public void MinimalEvidence_Constructs()
        {
            // Act
            EventEvidence evidence = Build();

            // Assert
            Assert.That(evidence.Family, Is.EqualTo(EventFamilies.Lifecycle));
            Assert.That(evidence.Subtype, Is.EqualTo("EXECUTION_STARTED"));
            Assert.That(evidence.Qualifiers, Is.Empty);
            Assert.That(evidence.CausalDepth, Is.EqualTo(0));
            Assert.That(evidence.Lineage, Is.EqualTo(EffectOriginLineage.Empty));
            Assert.That(evidence.SafetyStatus, Is.EqualTo(SafetyStatus.Normal));
            Assert.That(evidence.Payload, Is.Null);
        }

        [Test]
        public void FamilyNotInRegistry_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(family: "TELEMETRY"));
        }

        [Test]
        public void FamilyLowercase_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(family: "lifecycle"));
        }

        [Test]
        public void EmptySubtype_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(subtype: ""));
        }

        [Test]
        public void EmptyQualifierEntry_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(qualifiers: new List<string> { "" }));
        }

        [Test]
        public void NegativeCausalDepth_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(causalDepth: -1));
        }

        [Test]
        public void NullQualifiers_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new EventEvidence(
                EventFamilies.Lifecycle,
                "EXECUTION_STARTED",
                null,
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                null,
                null,
                SafetyStatus.Normal,
                null));
        }

        [Test]
        public void NullLineage_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new EventEvidence(
                EventFamilies.Lifecycle,
                "EXECUTION_STARTED",
                new List<string>(),
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                null,
                null,
                null,
                SafetyStatus.Normal,
                null));
        }

        [Test]
        public void EmptyCoreLineIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(coreLineIdentity: ""));
        }

        [Test]
        public void EmptyTargetIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(targetIdentity: ""));
        }

        [Test]
        public void EmptyDispositionReason_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(disposition: EventDisposition.Resolved, dispositionReason: ""));
        }

        [Test]
        public void EmptyAddedExecutionRequestIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(addedExecutionRequestIdentity: ""));
        }

        [Test]
        public void DispositionReasonWithoutDisposition_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(dispositionReason: "because"));
        }

        [Test]
        public void PlayerOwnedWithoutHostInstance_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(ownership: OwnershipClassification.PlayerOwned));
        }

        [Test]
        public void CoreOwnedWithoutCoreLineIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(ownership: OwnershipClassification.CoreOwned));
        }

        [Test]
        public void AddedExecutionRequestWithZeroDepth_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(addedExecutionRequestIdentity: "REQ:1", addedExecutionDepth: 0));
        }

        [Test]
        public void AddedExecutionDepthWithoutRequest_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(addedExecutionDepth: 2));
        }

        [Test]
        public void FullyPopulatedEvidence_RoundTripsEveryField()
        {
            // Arrange
            List<string> qualifiers = new() { "q1", "q2" };
            RuntimeUnitID containingUnit = new(3);
            TraceEventID parentEvent = new(1);
            TraceEventID causingEvent = new(2);
            InstanceID hostInstance = new(5);
            InstanceID effectOrigin = new(6);
            SourcePosition position = new(4);
            EffectOriginLineage lineage = EffectOriginLineage.Empty.Append(new InstanceID(5)).Append(new InstanceID(6));
            StructureContext structureContext = new(new InstanceID[] { new(7) }, "STRUCT:1", null, null);
            QuantityChangePayload payload = new(
                "VALUE:1",
                QuantityCategory.RuntimeVariable,
                QuantityOperationType.Increase,
                10,
                5,
                null,
                new List<QuantityModifierEvidence>(),
                null,
                10,
                15);

            // Act
            EventEvidence evidence = Build(
                family: EventFamilies.Quantity,
                subtype: "QUANTITY_CHANGED",
                qualifiers: qualifiers,
                causalDepth: 2,
                containingUnit: containingUnit,
                parentEvent: parentEvent,
                causingEvent: causingEvent,
                hostInstance: hostInstance,
                effectOriginInstance: effectOrigin,
                ownership: OwnershipClassification.PlayerOwned,
                position: position,
                targetIdentity: "TARGET:1",
                lineage: lineage,
                addedExecutionRequestIdentity: "REQ:1",
                addedExecutionDepth: 1,
                structureContext: structureContext,
                disposition: EventDisposition.Resolved,
                dispositionReason: "resolved cleanly",
                safetyStatus: SafetyStatus.Normal,
                payload: payload);

            // Assert
            Assert.That(evidence.Family, Is.EqualTo(EventFamilies.Quantity));
            Assert.That(evidence.Subtype, Is.EqualTo("QUANTITY_CHANGED"));
            Assert.That(evidence.Qualifiers, Is.EqualTo(qualifiers));
            Assert.That(evidence.CausalDepth, Is.EqualTo(2));
            Assert.That(evidence.ContainingUnit, Is.EqualTo(containingUnit));
            Assert.That(evidence.ParentEvent, Is.EqualTo(parentEvent));
            Assert.That(evidence.CausingEvent, Is.EqualTo(causingEvent));
            Assert.That(evidence.HostInstance, Is.EqualTo(hostInstance));
            Assert.That(evidence.EffectOriginInstance, Is.EqualTo(effectOrigin));
            Assert.That(evidence.Ownership, Is.EqualTo(OwnershipClassification.PlayerOwned));
            Assert.That(evidence.Position, Is.EqualTo(position));
            Assert.That(evidence.TargetIdentity, Is.EqualTo("TARGET:1"));
            Assert.That(evidence.Lineage, Is.EqualTo(lineage));
            Assert.That(evidence.AddedExecutionRequestIdentity, Is.EqualTo("REQ:1"));
            Assert.That(evidence.AddedExecutionDepth, Is.EqualTo(1));
            Assert.That(evidence.StructureContext, Is.EqualTo(structureContext));
            Assert.That(evidence.Disposition, Is.EqualTo(EventDisposition.Resolved));
            Assert.That(evidence.DispositionReason, Is.EqualTo("resolved cleanly"));
            Assert.That(evidence.Payload, Is.EqualTo(payload));
        }

        [Test]
        public void Equality_IsStructuralOverQualifiers()
        {
            // Arrange — independently built qualifier lists with identical content.
            EventEvidence left = Build(qualifiers: new List<string> { "q1", "q2" });
            EventEvidence right = Build(qualifiers: new List<string> { "q1", "q2" });

            // Act & Assert
            Assert.That(right, Is.EqualTo(left));
            Assert.That(right.GetHashCode(), Is.EqualTo(left.GetHashCode()));
        }

        private static EventEvidence Build(
            string family = EventFamilies.Lifecycle,
            string subtype = "EXECUTION_STARTED",
            IReadOnlyList<string> qualifiers = null,
            int causalDepth = 0,
            RuntimeUnitID? containingUnit = null,
            TraceEventID? parentEvent = null,
            TraceEventID? causingEvent = null,
            InstanceID? hostInstance = null,
            string coreLineIdentity = null,
            InstanceID? effectOriginInstance = null,
            OwnershipClassification? ownership = null,
            SourcePosition? position = null,
            string targetIdentity = null,
            EffectOriginLineage lineage = null,
            string addedExecutionRequestIdentity = null,
            int addedExecutionDepth = 0,
            StructureContext structureContext = null,
            EventDisposition? disposition = null,
            string dispositionReason = null,
            SafetyStatus safetyStatus = SafetyStatus.Normal,
            EventPayload payload = null
        )
        {
            return new EventEvidence(
                family,
                subtype,
                qualifiers ?? new List<string>(),
                causalDepth,
                containingUnit,
                parentEvent,
                causingEvent,
                hostInstance,
                coreLineIdentity,
                effectOriginInstance,
                ownership,
                position,
                targetIdentity,
                lineage ?? EffectOriginLineage.Empty,
                addedExecutionRequestIdentity,
                addedExecutionDepth,
                structureContext,
                disposition,
                dispositionReason,
                safetyStatus,
                payload);
        }
    }
}
