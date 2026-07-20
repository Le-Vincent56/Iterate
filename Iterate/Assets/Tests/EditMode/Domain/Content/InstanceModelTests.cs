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
            Assert.Throws<ArgumentException>(() => _ = new InstructionInstance(new InstanceID(1), null, null));
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
            InstructionInstance left = new(new InstanceID(5), _instruction, null);
            InstructionInstance right = new(new InstanceID(5), _instruction, null);

            Assert.AreEqual(left, right);
        }

        [Test]
        public void InstructionInstance_SameDefinitionDifferentID_AreNotEqual()
        {
            InstructionInstance first = new(new InstanceID(5), _instruction, null);
            InstructionInstance second = new(new InstanceID(6), _instruction, null);

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
        public void InstructionInstance_UnpatchedByDefault_AttachedPatchNull()
        {
            InstructionInstance instance = new(new InstanceID(1), _instruction, null);

            Assert.IsNull(instance.AttachedPatch);
        }

        [Test]
        public void InstructionInstance_WithPatchAttachment_PreservesInstanceID()
        {
            InstructionInstance unpatched = new(new InstanceID(4), _instruction, null);
            PatchInstance attached = new(new InstanceID(2), _patch);

            InstructionInstance patched = unpatched with { AttachedPatch = attached };

            Assert.AreEqual(new InstanceID(4), patched.InstanceID);
            Assert.AreEqual(attached, patched.AttachedPatch);
        }

        [Test]
        public void InstructionInstance_DifferentAttachedPatch_AreNotEqual()
        {
            InstructionInstance unpatched = new(new InstanceID(4), _instruction, null);
            InstructionInstance patched = unpatched with
            {
                AttachedPatch = new PatchInstance(new InstanceID(2), _patch)
            };

            Assert.AreNotEqual(unpatched, patched);
        }
    }
}
