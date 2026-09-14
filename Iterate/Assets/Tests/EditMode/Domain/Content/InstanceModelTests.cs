using System;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests the runtime instance model: the deterministic <see cref="InstanceIDSource"/> allocator and
    /// the immutable instance records that pair a frozen definition reference with an
    /// <see cref="InstanceID"/>. The definition/instance identity split is the behaviour under test —
    /// equal identity plus equal definition compare equal; one definition under two identities does not.
    /// </summary>
    public sealed class InstanceModelTests
    {
        private static readonly InstructionDefinition _instruction = new(
            new InstructionID("WB-INS-002"),
            "rules",
            "Add 1 to Value",
            ContentCategory.Instruction,
            Rarity.Common,
            Array.Empty<string>(),
            1,
            null,
            null,
            Array.Empty<string>());

        private static readonly StructureDefinition _structure = new(
            new StructureID("WB-STR-001"),
            "Repeat 2",
            "Repeat 2",
            ContentCategory.Structure,
            Rarity.Common,
            Array.Empty<string>(),
            2,
            StructureKind.Repeat,
            2,
            null);

        private static readonly DirectiveDefinition _directive = new(
            new DirectiveID("WB-DIR-004"),
            "Compile ahead",
            "Compile Ahead",
            ContentCategory.Directive,
            Rarity.Common,
            Array.Empty<string>(),
            Array.Empty<EffectDefinition>());

        private static readonly PatchDefinition _patch = new(
            new PatchID("WB-PAT-001"),
            "Patch",
            "Patch",
            ContentCategory.Patch,
            Rarity.Common,
            Array.Empty<string>(),
            null,
            Array.Empty<EffectDefinition>());

        [Test]
        public void Next_FirstCall_ReturnsOne()
        {
            InstanceIDSource source = new();

            Assert.AreEqual(new InstanceID(1), source.Next());
        }

        [Test]
        public void Next_IncrementsMonotonically()
        {
            InstanceIDSource source = new();

            Assert.AreEqual(new InstanceID(1), source.Next());
            Assert.AreEqual(new InstanceID(2), source.Next());
            Assert.AreEqual(new InstanceID(3), source.Next());
        }

        [Test]
        public void Next_TwoSources_AreIndependent()
        {
            InstanceIDSource first = new();
            InstanceIDSource second = new();

            first.Next();
            first.Next();

            Assert.AreEqual(new InstanceID(1), second.Next());
        }

        [Test]
        public void InstructionInstance_NullDefinition_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new InstructionInstance(new InstanceID(1), null, Array.Empty<PatchAttachment>()));
        }

        [Test]
        public void StructureInstance_NullDefinition_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new StructureInstance(new InstanceID(1), null));
        }

        [Test]
        public void DirectiveInstance_NullDefinition_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new DirectiveInstance(new InstanceID(1), null));
        }

        [Test]
        public void PatchInstance_NullDefinition_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new PatchInstance(new InstanceID(1), null));
        }

        [Test]
        public void InstructionInstance_EqualIDAndDefinition_AreEqual()
        {
            InstructionInstance left = new(new InstanceID(5), _instruction, Array.Empty<PatchAttachment>());
            InstructionInstance right = new(new InstanceID(5), _instruction, Array.Empty<PatchAttachment>());

            Assert.AreEqual(left, right);
        }

        [Test]
        public void InstructionInstance_SameDefinitionDifferentID_AreNotEqual()
        {
            InstructionInstance first = new(new InstanceID(5), _instruction, Array.Empty<PatchAttachment>());
            InstructionInstance second = new(new InstanceID(6), _instruction, Array.Empty<PatchAttachment>());

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void StructureInstance_EqualIDAndDefinition_AreEqual()
        {
            StructureInstance left = new(new InstanceID(9), _structure);
            StructureInstance right = new(new InstanceID(9), _structure);

            Assert.AreEqual(left, right);
        }

        [Test]
        public void StructureInstance_SameDefinitionDifferentID_AreNotEqual()
        {
            StructureInstance first = new(new InstanceID(9), _structure);
            StructureInstance second = new(new InstanceID(10), _structure);

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void DirectiveInstance_EqualIDAndDefinition_AreEqual()
        {
            DirectiveInstance left = new(new InstanceID(3), _directive);
            DirectiveInstance right = new(new InstanceID(3), _directive);

            Assert.AreEqual(left, right);
        }

        [Test]
        public void PatchInstance_EqualIDAndDefinition_AreEqual()
        {
            PatchInstance left = new(new InstanceID(2), _patch);
            PatchInstance right = new(new InstanceID(2), _patch);

            Assert.AreEqual(left, right);
        }

        [Test]
        public void InstructionInstance_EmptyAttachmentList_CarriesNoAttachments()
        {
            InstructionInstance instance = new(new InstanceID(1), _instruction, Array.Empty<PatchAttachment>());

            Assert.AreEqual(0, instance.AttachedPatches.Count);
        }

        [Test]
        public void InstructionInstance_NullAttachmentList_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new InstructionInstance(new InstanceID(1), _instruction, null));
        }

        [Test]
        public void InstructionInstance_DuplicateSockets_Throws()
        {
            PatchAttachment[] attachments =
            {
                new PatchAttachment(1, new PatchInstance(new InstanceID(2), _patch)),
                new PatchAttachment(1, new PatchInstance(new InstanceID(3), _patch))
            };

            Assert.Throws<ArgumentException>(() => _ = new InstructionInstance(new InstanceID(1), _instruction, attachments));
        }

        [Test]
        public void InstructionInstance_DescendingSockets_Throws()
        {
            PatchAttachment[] attachments =
            {
                new PatchAttachment(2, new PatchInstance(new InstanceID(2), _patch)),
                new PatchAttachment(1, new PatchInstance(new InstanceID(3), _patch))
            };

            Assert.Throws<ArgumentException>(() => _ = new InstructionInstance(new InstanceID(1), _instruction, attachments));
        }

        [Test]
        public void InstructionInstance_TwoAscendingSockets_KeepsSocketOrder()
        {
            PatchAttachment[] attachments =
            {
                new PatchAttachment(1, new PatchInstance(new InstanceID(2), _patch)),
                new PatchAttachment(2, new PatchInstance(new InstanceID(3), _patch))
            };

            InstructionInstance instance = new(new InstanceID(1), _instruction, attachments);

            Assert.AreEqual(2, instance.AttachedPatches.Count);
            Assert.AreEqual(1, instance.AttachedPatches[0].Socket);
            Assert.AreEqual(new InstanceID(2), instance.AttachedPatches[0].Patch.InstanceID);
            Assert.AreEqual(2, instance.AttachedPatches[1].Socket);
            Assert.AreEqual(new InstanceID(3), instance.AttachedPatches[1].Patch.InstanceID);
        }

        [Test]
        public void WithAttachment_EmptyHost_PreservesInstanceID()
        {
            InstructionInstance unpatched = new(new InstanceID(4), _instruction, Array.Empty<PatchAttachment>());
            PatchAttachment attachment = new(1, new PatchInstance(new InstanceID(2), _patch));

            InstructionInstance patched = unpatched.WithAttachment(attachment);

            Assert.AreEqual(new InstanceID(4), patched.InstanceID);
            Assert.AreEqual(1, patched.AttachedPatches.Count);
            Assert.AreEqual(attachment, patched.AttachedPatches[0]);
        }

        [Test]
        public void WithAttachment_OccupiedSocket_ReplacesThatSocketAndKeepsTheOther()
        {
            PatchAttachment[] attachments =
            {
                new PatchAttachment(1, new PatchInstance(new InstanceID(2), _patch)),
                new PatchAttachment(2, new PatchInstance(new InstanceID(3), _patch))
            };
            InstructionInstance patched = new(new InstanceID(4), _instruction, attachments);

            InstructionInstance replaced = patched.WithAttachment(
                new PatchAttachment(1, new PatchInstance(new InstanceID(9), _patch)));

            Assert.AreEqual(2, replaced.AttachedPatches.Count);
            Assert.AreEqual(new InstanceID(9), replaced.AttachedPatches[0].Patch.InstanceID);
            Assert.AreEqual(new InstanceID(3), replaced.AttachedPatches[1].Patch.InstanceID);
        }

        [Test]
        public void WithAttachment_LowerSocketAfterHigher_KeepsSocketsAscending()
        {
            InstructionInstance first = new InstructionInstance(new InstanceID(4), _instruction, Array.Empty<PatchAttachment>())
                .WithAttachment(new PatchAttachment(2, new PatchInstance(new InstanceID(3), _patch)));

            InstructionInstance both = first.WithAttachment(
                new PatchAttachment(1, new PatchInstance(new InstanceID(2), _patch)));

            Assert.AreEqual(1, both.AttachedPatches[0].Socket);
            Assert.AreEqual(2, both.AttachedPatches[1].Socket);
        }

        [Test]
        public void WithoutAttachment_OccupiedSocket_RemovesOnlyThatSocket()
        {
            PatchAttachment[] attachments =
            {
                new PatchAttachment(1, new PatchInstance(new InstanceID(2), _patch)),
                new PatchAttachment(2, new PatchInstance(new InstanceID(3), _patch))
            };
            InstructionInstance patched = new(new InstanceID(4), _instruction, attachments);

            InstructionInstance stripped = patched.WithoutAttachment(1);

            Assert.AreEqual(1, stripped.AttachedPatches.Count);
            Assert.AreEqual(2, stripped.AttachedPatches[0].Socket);
        }

        [Test]
        public void WithoutAttachment_EmptySocket_ReturnsAnEqualRecord()
        {
            PatchAttachment[] attachments =
            {
                new PatchAttachment(1, new PatchInstance(new InstanceID(2), _patch))
            };
            InstructionInstance patched = new(new InstanceID(4), _instruction, attachments);

            InstructionInstance unchanged = patched.WithoutAttachment(2);

            Assert.AreEqual(patched, unchanged);
        }

        [Test]
        public void TryGetAttachment_OccupiedSocket_AnswersBySocket()
        {
            PatchAttachment[] attachments =
            {
                new PatchAttachment(2, new PatchInstance(new InstanceID(3), _patch))
            };
            InstructionInstance patched = new(new InstanceID(4), _instruction, attachments);

            Assert.IsTrue(patched.TryGetAttachment(2, out PatchAttachment found));
            Assert.AreEqual(new InstanceID(3), found.Patch.InstanceID);
            Assert.IsFalse(patched.TryGetAttachment(1, out PatchAttachment missing));
            Assert.IsNull(missing);
        }

        [Test]
        public void InstructionInstance_DifferentAttachments_AreNotEqual()
        {
            InstructionInstance unpatched = new(new InstanceID(4), _instruction, Array.Empty<PatchAttachment>());
            InstructionInstance patched = unpatched.WithAttachment(
                new PatchAttachment(1, new PatchInstance(new InstanceID(2), _patch)));

            Assert.AreNotEqual(unpatched, patched);
        }

        [Test]
        public void InstructionInstance_SameAttachments_AreEqual()
        {
            InstructionInstance left = new InstructionInstance(new InstanceID(4), _instruction, Array.Empty<PatchAttachment>())
                .WithAttachment(new PatchAttachment(1, new PatchInstance(new InstanceID(2), _patch)));
            InstructionInstance right = new InstructionInstance(new InstanceID(4), _instruction, Array.Empty<PatchAttachment>())
                .WithAttachment(new PatchAttachment(1, new PatchInstance(new InstanceID(2), _patch)));

            Assert.AreEqual(left, right);
        }
    }
}
