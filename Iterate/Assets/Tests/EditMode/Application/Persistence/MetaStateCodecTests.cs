using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.Persistence;

namespace Iterate.Application.Persistence.Tests
{
    /// <summary>
    /// Tests <see cref="MetaState"/> and its codec: the onboarding-completion flag that survives between
    /// Sessions, its two derivations, and a codec that treats an absent member as a defect rather than as
    /// false. The distinction matters here more than anywhere: defaulting a missing flag to false would
    /// silently replay the onboarding for a player who had finished it.
    /// </summary>
    public sealed class MetaStateCodecTests
    {
        [Test]
        public void Default_HasNotCompletedOnboarding()
        {
            Assert.IsFalse(MetaState.Default.OnboardingComplete);
        }

        [Test]
        public void WithOnboardingComplete_SetsTheFlagWithoutMutatingTheSource()
        {
            MetaState source = MetaState.Default;

            MetaState derived = source.WithOnboardingComplete();

            Assert.IsTrue(derived.OnboardingComplete);
            Assert.IsFalse(source.OnboardingComplete, "a derivation leaves the value it came from alone.");
        }

        [Test]
        public void ResetOnboarding_ClearsTheFlagWithoutMutatingTheSource()
        {
            MetaState source = MetaState.Default.WithOnboardingComplete();

            MetaState derived = source.ResetOnboarding();

            Assert.IsFalse(derived.OnboardingComplete);
            Assert.IsTrue(source.OnboardingComplete);
        }

        [Test]
        public void ToJson_WritesTheFlagUnderItsCanonicalName()
        {
            JsonObject payload = MetaStateCodec.ToJson(MetaState.Default.WithOnboardingComplete());

            Assert.AreEqual(1, payload.Keys.Count);
            Assert.AreEqual("onboardingComplete", payload.Keys[0]);
            Assert.IsTrue(payload.TryGet("onboardingComplete", out JsonValue flag));
            Assert.IsTrue(((JsonBool)flag).Value);
        }

        [Test]
        public void FromJson_ReadsBothValues([Values(true, false)] bool complete)
        {
            JsonObject payload = new JsonObjectBuilder().Add("onboardingComplete", complete).Build();

            CodecResult<MetaState> result = MetaStateCodec.FromJson(payload);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(complete, result.Value.OnboardingComplete);
            Assert.IsNull(result.Defect);
        }

        [Test]
        public void FromJson_AnAbsentFlag_IsADefectNamingTheMember()
        {
            CodecResult<MetaState> result = MetaStateCodec.FromJson(new JsonObjectBuilder().Build());

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Value, "a defective payload yields no state at all.");
            StringAssert.Contains("onboardingComplete", result.Defect);
        }

        [Test]
        public void FromJson_AWrongKindedFlag_IsADefect()
        {
            JsonObject payload = new JsonObjectBuilder().Add("onboardingComplete", "true").Build();

            CodecResult<MetaState> result = MetaStateCodec.FromJson(payload);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("onboardingComplete", result.Defect);
        }

        [Test]
        public void WriteThenRead_RoundTripsThroughTheEnvelope([Values(true, false)] bool complete)
        {
            MetaState state = complete ? MetaState.Default.WithOnboardingComplete() : MetaState.Default;
            string text = VersionedDocument.Write(SaveFiles.FormatVersion, MetaStateCodec.ToJson(state));

            VersionedDocumentReadResult read = VersionedDocument.Read(text, SaveFiles.FormatVersion);
            CodecResult<MetaState> result = MetaStateCodec.FromJson(read.Payload);

            Assert.AreEqual(VersionedDocumentOutcome.Loaded, read.Outcome);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(state, result.Value);
        }

        [Test]
        public void Write_PlacesTheVersionBeforeTheFlag()
        {
            string text = VersionedDocument.Write(
                SaveFiles.FormatVersion,
                MetaStateCodec.ToJson(MetaState.Default.WithOnboardingComplete())
            );

            Assert.AreEqual("{\n  \"formatVersion\": 1,\n  \"onboardingComplete\": true\n}\n", text);
        }

        [Test]
        public void SaveFiles_NameTheThreeFilesAndTheFormatVersion()
        {
            Assert.AreEqual(1, SaveFiles.FormatVersion);
            Assert.AreEqual("settings.json", SaveFiles.Settings);
            Assert.AreEqual("meta.json", SaveFiles.Meta);
            Assert.AreEqual("checkpoint.json", SaveFiles.Checkpoint);
        }
    }
}
