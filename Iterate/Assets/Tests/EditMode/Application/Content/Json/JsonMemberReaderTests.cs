using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Domain.Progression;

namespace Iterate.Application.Content.Json.Tests
{
    /// <summary>
    /// Tests the fail-fast <see cref="JsonMemberReader"/>: an absent or wrong-kinded member is a defect
    /// naming <c>path.key</c> rather than a default value (TA-DAT-002, Design §31.3), the first defect
    /// wins and every later accessor returns a default, and a defect recorded on a
    /// <see cref="JsonMemberReader.Child"/> surfaces on the reader its codec checks.
    /// </summary>
    public sealed class JsonMemberReaderTests
    {
        private static JsonMemberReader Reader(JsonObject source)
        {
            return new JsonMemberReader(source, "root");
        }

        private static JsonObject Sample()
        {
            List<JsonValue> ordinals = new() { JsonValues.Integer(1L), JsonValues.Integer(2L) };
            JsonObject economy = new JsonObjectBuilder().Add("tokens", 12L).Build();

            return new JsonObjectBuilder()
                .Add("name", "WB-PROC-004")
                .Add("nullable", (string)null)
                .Add("count", 7L)
                .Add("big", 5_000_000_000L)
                .Add("flag", true)
                .Add("fraction", JsonValues.Double(1.5d))
                .Add("ordinals", JsonValues.Array(ordinals))
                .Add("economy", economy)
                .AddNull("branch")
                .Add("origin", "Purchase")
                .Build();
        }

        [Test]
        public void FreshReader_HasNoDefect()
        {
            JsonMemberReader reader = Reader(Sample());

            Assert.IsFalse(reader.Failed);
            Assert.IsNull(reader.Defect);
        }

        [Test]
        public void String_ReadsTheValue()
        {
            JsonMemberReader reader = Reader(Sample());

            Assert.AreEqual("WB-PROC-004", reader.String("name"));
            Assert.IsFalse(reader.Failed);
        }

        [Test]
        public void String_AbsentKey_RecordsPathKeyAndReason()
        {
            JsonMemberReader reader = Reader(Sample());

            string value = reader.String("missing");

            Assert.IsNull(value);
            Assert.IsTrue(reader.Failed);
            Assert.AreEqual("root.missing: the member is absent.", reader.Defect);
        }

        [Test]
        public void String_WrongKind_IsADefect()
        {
            JsonMemberReader reader = Reader(Sample());

            reader.String("count");

            Assert.IsTrue(reader.Failed);
            Assert.AreEqual("root.count: expected a string.", reader.Defect);
        }

        [Test]
        public void String_JsonNull_IsADefectBecauseItIsRequired()
        {
            JsonMemberReader reader = Reader(Sample());

            reader.String("nullable");

            Assert.IsTrue(reader.Failed);
            Assert.AreEqual("root.nullable: expected a string.", reader.Defect);
        }

        [Test]
        public void OptionalString_JsonNull_IsNullWithoutADefect()
        {
            JsonMemberReader reader = Reader(Sample());

            Assert.IsNull(reader.OptionalString("nullable"));
            Assert.IsFalse(reader.Failed);
        }

        [Test]
        public void OptionalString_AbsentKey_IsADefect()
        {
            JsonMemberReader reader = Reader(Sample());

            reader.OptionalString("missing");

            Assert.IsTrue(reader.Failed);
            Assert.AreEqual("root.missing: the member is absent.", reader.Defect);
        }

        [Test]
        public void Int_ReadsAnIntegerNumber()
        {
            JsonMemberReader reader = Reader(Sample());

            Assert.AreEqual(7, reader.Int("count"));
            Assert.IsFalse(reader.Failed);
        }

        [Test]
        public void Int_NonIntegerNumber_IsADefect()
        {
            JsonMemberReader reader = Reader(Sample());

            Assert.AreEqual(0, reader.Int("fraction"));
            Assert.IsTrue(reader.Failed);
            Assert.AreEqual("root.fraction: expected an integer.", reader.Defect);
        }

        [Test]
        public void Int_OutsideTheInt32Range_IsADefect()
        {
            JsonMemberReader reader = Reader(Sample());

            Assert.AreEqual(0, reader.Int("big"));
            Assert.IsTrue(reader.Failed);
            Assert.AreEqual("root.big: the integer is outside the range of System.Int32.", reader.Defect);
        }

        [Test]
        public void OptionalInt_JsonNull_IsNullWithoutADefect()
        {
            JsonMemberReader reader = Reader(Sample());

            Assert.IsNull(reader.OptionalInt("branch"));
            Assert.IsFalse(reader.Failed);
        }

        [Test]
        public void OptionalInt_AbsentKey_IsADefect()
        {
            JsonMemberReader reader = Reader(Sample());

            reader.OptionalInt("missing");

            Assert.IsTrue(reader.Failed);
            Assert.AreEqual("root.missing: the member is absent.", reader.Defect);
        }

        [Test]
        public void Long_ReadsBeyondTheInt32Range()
        {
            JsonMemberReader reader = Reader(Sample());

            Assert.AreEqual(5_000_000_000L, reader.Long("big"));
            Assert.IsFalse(reader.Failed);
        }

        [Test]
        public void Bool_ReadsTheValueAndRejectsAWrongKind()
        {
            JsonMemberReader reader = Reader(Sample());
            Assert.IsTrue(reader.Bool("flag"));
            Assert.IsFalse(reader.Failed);

            JsonMemberReader wrong = Reader(Sample());
            Assert.IsFalse(wrong.Bool("name"));
            Assert.IsTrue(wrong.Failed);
            Assert.AreEqual("root.name: expected a boolean.", wrong.Defect);
        }

        [Test]
        public void Array_ReadsTheItemsAndRejectsAWrongKind()
        {
            JsonMemberReader reader = Reader(Sample());
            JsonArray ordinals = reader.Array("ordinals");
            Assert.AreEqual(2, ordinals.Items.Count);
            Assert.IsFalse(reader.Failed);

            JsonMemberReader wrong = Reader(Sample());
            Assert.IsNull(wrong.Array("name"));
            Assert.IsTrue(wrong.Failed);
            Assert.AreEqual("root.name: expected an array.", wrong.Defect);
        }

        [Test]
        public void OptionalArray_JsonNull_IsNullWithoutADefect()
        {
            JsonMemberReader reader = Reader(Sample());

            Assert.IsNull(reader.OptionalArray("branch"));
            Assert.IsFalse(reader.Failed);
        }

        [Test]
        public void Object_ReadsTheObjectAndRejectsAWrongKind()
        {
            JsonMemberReader reader = Reader(Sample());
            JsonObject economy = reader.Object("economy");
            Assert.AreEqual(1, economy.Keys.Count);
            Assert.IsFalse(reader.Failed);

            JsonMemberReader wrong = Reader(Sample());
            Assert.IsNull(wrong.Object("name"));
            Assert.IsTrue(wrong.Failed);
            Assert.AreEqual("root.name: expected an object.", wrong.Defect);
        }

        [Test]
        public void OptionalObject_JsonNull_IsNullWithoutADefect()
        {
            JsonMemberReader reader = Reader(Sample());

            Assert.IsNull(reader.OptionalObject("branch"));
            Assert.IsFalse(reader.Failed);
        }

        [Test]
        public void Child_ExtendsThePathOfItsOwnDefects()
        {
            JsonMemberReader reader = Reader(Sample());

            JsonMemberReader child = reader.Child("economy");
            child.String("missing");

            Assert.AreEqual("root.economy.missing: the member is absent.", child.Defect);
        }

        [Test]
        public void Child_ReadsItsOwnMembers()
        {
            JsonMemberReader reader = Reader(Sample());

            JsonMemberReader child = reader.Child("economy");

            Assert.AreEqual(12, child.Int("tokens"));
            Assert.IsFalse(reader.Failed);
        }

        [Test]
        public void Child_Defect_SurfacesOnTheParentTheCodecChecks()
        {
            JsonMemberReader reader = Reader(Sample());

            reader.Child("economy").String("missing");

            Assert.IsTrue(reader.Failed);
            Assert.AreEqual("root.economy.missing: the member is absent.", reader.Defect);
        }

        [Test]
        public void Enum_ReadsAMemberName()
        {
            JsonMemberReader reader = Reader(Sample());

            Assert.AreEqual(AcquisitionOrigin.Purchase, reader.Enum<AcquisitionOrigin>("origin"));
            Assert.IsFalse(reader.Failed);
        }

        [Test]
        public void Enum_WrongCase_IsADefect()
        {
            JsonObject source = new JsonObjectBuilder().Add("origin", "purchase").Build();
            JsonMemberReader reader = Reader(source);

            reader.Enum<AcquisitionOrigin>("origin");

            Assert.IsTrue(reader.Failed);
            Assert.AreEqual("root.origin: 'purchase' is not a member of AcquisitionOrigin.", reader.Defect);
        }

        [Test]
        public void Enum_NumericName_IsADefect()
        {
            JsonObject source = new JsonObjectBuilder().Add("origin", "2").Build();
            JsonMemberReader reader = Reader(source);

            reader.Enum<AcquisitionOrigin>("origin");

            Assert.IsTrue(reader.Failed);
            Assert.AreEqual("root.origin: '2' is not a member of AcquisitionOrigin.", reader.Defect);
        }

        [Test]
        public void OptionalEnum_JsonNull_IsNullWithoutADefect()
        {
            JsonMemberReader reader = Reader(Sample());

            Assert.IsNull(reader.OptionalEnum<AcquisitionOrigin>("branch"));
            Assert.IsFalse(reader.Failed);
        }

        [Test]
        public void OptionalEnum_AbsentKey_IsADefect()
        {
            JsonMemberReader reader = Reader(Sample());

            reader.OptionalEnum<AcquisitionOrigin>("missing");

            Assert.IsTrue(reader.Failed);
            Assert.AreEqual("root.missing: the member is absent.", reader.Defect);
        }

        [Test]
        public void FirstDefectWins_ASecondDoesNotOverwriteIt()
        {
            JsonMemberReader reader = Reader(Sample());

            reader.String("first");
            reader.Int("second");

            Assert.AreEqual("root.first: the member is absent.", reader.Defect);
        }

        [Test]
        public void AfterAFailure_EveryAccessorReturnsItsDefault()
        {
            JsonMemberReader reader = Reader(Sample());
            reader.String("missing");

            Assert.IsNull(reader.String("name"));
            Assert.IsNull(reader.OptionalString("name"));
            Assert.AreEqual(0, reader.Int("count"));
            Assert.IsNull(reader.OptionalInt("count"));
            Assert.AreEqual(0L, reader.Long("big"));
            Assert.IsFalse(reader.Bool("flag"));
            Assert.IsNull(reader.Array("ordinals"));
            Assert.IsNull(reader.OptionalArray("ordinals"));
            Assert.IsNull(reader.Object("economy"));
            Assert.IsNull(reader.OptionalObject("economy"));
            Assert.AreEqual(default(AcquisitionOrigin), reader.Enum<AcquisitionOrigin>("origin"));
            Assert.IsNull(reader.OptionalEnum<AcquisitionOrigin>("origin"));
        }

        [Test]
        public void Fail_RecordsACodecLevelCrossCheckDefect()
        {
            JsonMemberReader reader = Reader(Sample());

            reader.Fail("hash", "the stored hash does not match the derived one.");

            Assert.IsTrue(reader.Failed);
            Assert.AreEqual("root.hash: the stored hash does not match the derived one.", reader.Defect);
        }
    }
}
