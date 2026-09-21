using System;
using NUnit.Framework;
using Iterate.Application.Content.Json;

namespace Iterate.Application.Content.Json.Tests
{
    /// <summary>
    /// Tests the <see cref="VersionedDocument"/> envelope every saved file is written inside: the format
    /// version is stamped first from the very first write, and reading separates a file that is not a
    /// versioned document at all from one written by a future build. Malformed text is an outcome, never
    /// an exception — a save file is data the game found on disk, not a contract a caller broke.
    /// </summary>
    public sealed class VersionedDocumentTests
    {
        [Test]
        public void Write_AnEmptyPayload_IsTheVersionAlone()
        {
            string text = VersionedDocument.Write(1, new JsonObjectBuilder().Build());

            Assert.AreEqual("{\n  \"formatVersion\": 1\n}\n", text);
        }

        [Test]
        public void Write_PutsTheVersionFirstAndKeepsPayloadOrder()
        {
            JsonObject payload = new JsonObjectBuilder()
                .Add("zebra", 1L)
                .Add("apple", true)
                .Build();

            string text = VersionedDocument.Write(1, payload);

            Assert.AreEqual(
                "{\n  \"formatVersion\": 1,\n  \"zebra\": 1,\n  \"apple\": true\n}\n",
                text
            );
        }

        [Test]
        public void Write_APayloadCarryingTheVersionMember_Throws()
        {
            JsonObject payload = new JsonObjectBuilder().Add(VersionedDocument.VersionMember, 1L).Build();

            Assert.Throws<ArgumentException>(() => VersionedDocument.Write(1, payload));
        }

        [Test]
        public void VersionMember_IsTheCamelCaseName()
        {
            Assert.AreEqual("formatVersion", VersionedDocument.VersionMember);
        }

        [Test]
        public void Read_OfAWrittenDocument_IsLoaded()
        {
            JsonObject payload = new JsonObjectBuilder().Add("onboardingComplete", true).Build();
            string text = VersionedDocument.Write(1, payload);

            VersionedDocumentReadResult result = VersionedDocument.Read(text, 1);

            Assert.AreEqual(VersionedDocumentOutcome.Loaded, result.Outcome);
            Assert.AreEqual(1, result.FormatVersion);
            Assert.IsNull(result.Reason);
            Assert.IsTrue(result.Payload.TryGet("onboardingComplete", out JsonValue flag));
            Assert.IsTrue(((JsonBool)flag).Value);
        }

        [Test]
        public void Read_KeepsTheVersionMemberInThePayload()
        {
            // The payload is the whole object rather than a copy with the version stripped, so a codec
            // reads past `formatVersion` rather than the envelope rebuilding the tree to hide it.
            string text = VersionedDocument.Write(1, new JsonObjectBuilder().Add("a", 1L).Build());

            VersionedDocumentReadResult result = VersionedDocument.Read(text, 1);

            Assert.AreEqual(2, result.Payload.Keys.Count);
            Assert.AreEqual(VersionedDocument.VersionMember, result.Payload.Keys[0]);
        }

        [Test]
        public void Read_OfTextThatIsNotJson_IsMalformed()
        {
            VersionedDocumentReadResult result = VersionedDocument.Read("not json", 1);

            AssertMalformed(result);
        }

        [Test]
        public void Read_OfEmptyOrNullText_IsMalformed([Values("", null)] string text)
        {
            AssertMalformed(VersionedDocument.Read(text, 1));
        }

        [Test]
        public void Read_OfANonObjectRoot_IsMalformed([Values("[]", "1", "\"text\"", "null")] string text)
        {
            AssertMalformed(VersionedDocument.Read(text, 1));
        }

        [Test]
        public void Read_OfAnObjectWithoutTheVersion_IsMalformed()
        {
            AssertMalformed(VersionedDocument.Read("{}", 1));
        }

        [Test]
        public void Read_OfANonIntegerVersion_IsMalformed([Values("\"1\"", "1.5", "true", "null")] string version)
        {
            AssertMalformed(VersionedDocument.Read("{\"formatVersion\": " + version + "}", 1));
        }

        [Test]
        public void Read_OfAFutureVersion_IsUnsupportedAndCarriesTheVersionFound()
        {
            VersionedDocumentReadResult result = VersionedDocument.Read("{\"formatVersion\": 2}", 1);

            Assert.AreEqual(VersionedDocumentOutcome.UnsupportedVersion, result.Outcome);
            Assert.AreEqual(2, result.FormatVersion);
            Assert.IsNull(result.Payload);
            Assert.IsNotNull(result.Reason);
        }

        [Test]
        public void Read_OfAnIntegerBeyondTheInt64Range_IsMalformedRatherThanAThrow()
        {
            // The reader parses an integer literal with long.Parse, which throws OverflowException rather
            // than its own parse exception. A save file is data found on disk, so a twenty-digit number
            // must read as Malformed rather than take the load path down with it.
            string text = "{\"formatVersion\": 1, \"big\": 99999999999999999999}";

            AssertMalformed(VersionedDocument.Read(text, 1));
        }

        [Test]
        public void Read_OfADuplicateKey_IsMalformed()
        {
            AssertMalformed(VersionedDocument.Read("{\"formatVersion\": 1, \"formatVersion\": 1}", 1));
        }

        [Test]
        public void Read_OfTrailingContent_IsMalformed()
        {
            AssertMalformed(VersionedDocument.Read("{\"formatVersion\": 1} trailing", 1));
        }

        [Test]
        public void Read_AgainstADifferentExpectedVersion_ComparesAgainstThatOne()
        {
            VersionedDocumentReadResult supported = VersionedDocument.Read("{\"formatVersion\": 2}", 2);
            VersionedDocumentReadResult unsupported = VersionedDocument.Read("{\"formatVersion\": 1}", 2);

            Assert.AreEqual(VersionedDocumentOutcome.Loaded, supported.Outcome);
            Assert.AreEqual(VersionedDocumentOutcome.UnsupportedVersion, unsupported.Outcome);
            Assert.AreEqual(1, unsupported.FormatVersion);
        }

        [Test]
        public void WriteThenRead_RoundTripsAPayloadOfEveryValueKind()
        {
            JsonObject payload = new JsonObjectBuilder()
                .Add("text", "value")
                .Add("count", 7L)
                .Add("flag", false)
                .AddNull("absent")
                .Build();

            VersionedDocumentReadResult result = VersionedDocument.Read(VersionedDocument.Write(1, payload), 1);

            Assert.AreEqual(VersionedDocumentOutcome.Loaded, result.Outcome);
            Assert.AreEqual(5, result.Payload.Keys.Count);
            Assert.IsTrue(result.Payload.TryGet("absent", out JsonValue absent));
            Assert.IsInstanceOf<JsonNull>(absent);
        }

        /// <summary>
        /// Asserts a read reported malformed text, carrying a reason and no payload or version.
        /// </summary>
        /// <param name="result">The read result.</param>
        private static void AssertMalformed(VersionedDocumentReadResult result)
        {
            Assert.AreEqual(VersionedDocumentOutcome.Malformed, result.Outcome);
            Assert.IsNull(result.Payload);
            Assert.AreEqual(0, result.FormatVersion);
            Assert.IsNotNull(result.Reason, "a malformed document says what was wrong with it.");
        }
    }
}
