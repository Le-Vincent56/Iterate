using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.Persistence;

namespace Iterate.Application.Persistence.Tests
{
    /// <summary>
    /// Tests the <see cref="SettingsState"/> shell and its codec. Settings ships with no preference
    /// fields yet, so the codec's job today is to prove the shape is versioned and round-trips: a file
    /// carrying nothing but its format version loads as the default rather than failing, which is what
    /// lets a later build add a field without every existing save becoming unreadable.
    /// </summary>
    public sealed class SettingsStateCodecTests
    {
        [Test]
        public void Default_IsASingleSharedValue()
        {
            Assert.IsNotNull(SettingsState.Default);
            Assert.AreEqual(SettingsState.Default, SettingsState.Default);
        }

        [Test]
        public void ToJson_OfTheDefault_IsAnEmptyObject()
        {
            JsonObject payload = SettingsStateCodec.ToJson(SettingsState.Default);

            Assert.AreEqual(0, payload.Keys.Count);
        }

        [Test]
        public void FromJson_OfAnEmptyObject_IsTheDefault()
        {
            CodecResult<SettingsState> result = SettingsStateCodec.FromJson(new JsonObjectBuilder().Build());

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SettingsState.Default, result.Value);
            Assert.IsNull(result.Defect);
        }

        [Test]
        public void FromJson_OfAPayloadCarryingOnlyTheFormatVersion_IsTheDefault()
        {
            JsonObject payload = new JsonObjectBuilder().Add(VersionedDocument.VersionMember, 1L).Build();

            CodecResult<SettingsState> result = SettingsStateCodec.FromJson(payload);

            Assert.IsTrue(result.Succeeded, "the envelope leaves its own member in the payload; a codec reads past it.");
            Assert.AreEqual(SettingsState.Default, result.Value);
        }

        [Test]
        public void WriteThenRead_RoundTripsThroughTheEnvelope()
        {
            string text = VersionedDocument.Write(SaveFiles.FormatVersion, SettingsStateCodec.ToJson(SettingsState.Default));

            VersionedDocumentReadResult read = VersionedDocument.Read(text, SaveFiles.FormatVersion);
            CodecResult<SettingsState> result = SettingsStateCodec.FromJson(read.Payload);

            Assert.AreEqual(VersionedDocumentOutcome.Loaded, read.Outcome);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SettingsState.Default, result.Value);
        }

        [Test]
        public void Write_IsExactlyTheVersionAlone()
        {
            string text = VersionedDocument.Write(SaveFiles.FormatVersion, SettingsStateCodec.ToJson(SettingsState.Default));

            Assert.AreEqual("{\n  \"formatVersion\": 1\n}\n", text);
        }
    }
}
