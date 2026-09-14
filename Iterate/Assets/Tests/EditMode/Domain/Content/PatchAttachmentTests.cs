using System;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests <see cref="PatchAttachment"/>, the socket-plus-Patch pair an Instruction instance carries
    /// once per occupied socket. The socket discriminant is what makes two attachments of one Patch
    /// definition on one host distinguishable, so socket validity is the behaviour under test.
    /// </summary>
    public sealed class PatchAttachmentTests
    {
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
        public void Constructor_SocketOne_Constructs()
        {
            PatchAttachment attachment = new(1, new PatchInstance(new InstanceID(2), _patch));

            Assert.AreEqual(1, attachment.Socket);
            Assert.AreEqual(new InstanceID(2), attachment.Patch.InstanceID);
        }

        [Test]
        public void Constructor_SocketZero_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => _ = new PatchAttachment(0, new PatchInstance(new InstanceID(2), _patch)));
        }

        [Test]
        public void Constructor_NegativeSocket_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => _ = new PatchAttachment(-1, new PatchInstance(new InstanceID(2), _patch)));
        }

        [Test]
        public void Constructor_NullPatch_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new PatchAttachment(1, null));
        }

        [Test]
        public void Equality_SameSocketAndPatchInstance_AreEqual()
        {
            PatchAttachment left = new(1, new PatchInstance(new InstanceID(2), _patch));
            PatchAttachment right = new(1, new PatchInstance(new InstanceID(2), _patch));

            Assert.AreEqual(left, right);
        }

        [Test]
        public void Equality_SamePatchInstanceDifferentSocket_AreNotEqual()
        {
            PatchInstance instance = new(new InstanceID(2), _patch);
            PatchAttachment left = new(1, instance);
            PatchAttachment right = new(2, instance);

            Assert.AreNotEqual(left, right);
        }
    }
}
