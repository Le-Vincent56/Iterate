using NUnit.Framework;
using Iterate.Application.Content.Json;

namespace Iterate.Application.Content.Json.Tests
{
    /// <summary>
    /// Tests the strict deterministic <see cref="CatalogJsonReader"/>: valid values round-trip with
    /// correct structure, numbers, and positions; every strictness rejection throws
    /// <see cref="CatalogJsonParseException"/> with a located line and column.
    /// </summary>
    public sealed class CatalogJsonReaderTests
    {
        private static CatalogJsonReader Reader()
        {
            return new CatalogJsonReader();
        }

        [Test]
        public void Parse_Object_ExposesKeysInDocumentOrder()
        {
            JsonValue root = Reader().Parse("{\"b\": 1, \"a\": 2, \"c\": 3}");

            JsonObject obj = (JsonObject)root;
            Assert.AreEqual(3, obj.Keys.Count);
            Assert.AreEqual("b", obj.Keys[0]);
            Assert.AreEqual("a", obj.Keys[1]);
            Assert.AreEqual("c", obj.Keys[2]);
        }

        [Test]
        public void Parse_Object_TryGetHitAndMiss()
        {
            JsonObject obj = (JsonObject)Reader().Parse("{\"name\": \"value\"}");

            Assert.IsTrue(obj.TryGet("name", out JsonValue found));
            Assert.AreEqual("value", ((JsonString)found).Value);
            Assert.IsFalse(obj.TryGet("absent", out JsonValue missing));
            Assert.IsNull(missing);
        }

        [Test]
        public void Parse_EmptyObjectAndArray()
        {
            JsonObject obj = (JsonObject)Reader().Parse("{}");
            JsonArray array = (JsonArray)Reader().Parse("[]");

            Assert.AreEqual(0, obj.Keys.Count);
            Assert.AreEqual(0, array.Items.Count);
        }

        [Test]
        public void Parse_Array_PreservesItemOrder()
        {
            JsonArray array = (JsonArray)Reader().Parse("[10, 20, 30]");

            Assert.AreEqual(3, array.Items.Count);
            Assert.AreEqual(10, ((JsonNumber)array.Items[0]).IntegerValue);
            Assert.AreEqual(30, ((JsonNumber)array.Items[2]).IntegerValue);
        }

        [Test]
        public void Parse_Nested_ObjectInArray()
        {
            JsonArray array = (JsonArray)Reader().Parse("[{\"k\": true}]");

            JsonObject inner = (JsonObject)array.Items[0];
            Assert.IsTrue(inner.TryGet("k", out JsonValue flag));
            Assert.IsTrue(((JsonBool)flag).Value);
        }

        [Test]
        public void Parse_String_DecodesStandardEscapes()
        {
            JsonString value = (JsonString)Reader().Parse("\"a\\\"b\\\\c\\n\\t\\u0041\"");

            Assert.AreEqual("a\"b\\c\n\tA", value.Value);
        }

        [Test]
        public void Parse_Integer_ReportsIntegerForm()
        {
            JsonNumber number = (JsonNumber)Reader().Parse("42");

            Assert.IsTrue(number.IsInteger);
            Assert.AreEqual(42L, number.IntegerValue);
        }

        [Test]
        public void Parse_NegativeInteger()
        {
            JsonNumber number = (JsonNumber)Reader().Parse("-7");

            Assert.IsTrue(number.IsInteger);
            Assert.AreEqual(-7L, number.IntegerValue);
        }

        [Test]
        public void Parse_Fractional_ReportsNonInteger()
        {
            JsonNumber number = (JsonNumber)Reader().Parse("1.75");

            Assert.IsFalse(number.IsInteger);
            Assert.AreEqual(1.75d, number.DoubleValue, 0.0000001d);
        }

        [Test]
        public void Parse_Exponent_ReportsNonInteger()
        {
            JsonNumber number = (JsonNumber)Reader().Parse("2e3");

            Assert.IsFalse(number.IsInteger);
            Assert.AreEqual(2000d, number.DoubleValue, 0.0000001d);
        }

        [Test]
        public void Parse_Zero_IsInteger()
        {
            JsonNumber number = (JsonNumber)Reader().Parse("0");

            Assert.IsTrue(number.IsInteger);
            Assert.AreEqual(0L, number.IntegerValue);
        }

        [Test]
        public void Parse_BoolAndNull()
        {
            Assert.IsFalse(((JsonBool)Reader().Parse("false")).Value);
            Assert.IsInstanceOf<JsonNull>(Reader().Parse("null"));
        }

        [Test]
        public void Parse_TracksLineAndColumnOfNestedValue()
        {
            JsonObject root = (JsonObject)Reader().Parse("{\n    \"key\": 5\n}");

            Assert.IsTrue(root.TryGet("key", out JsonValue value));
            Assert.AreEqual(2, value.Line);
            Assert.AreEqual(12, value.Column);
        }

        [Test]
        public void Parse_ToleratesLeadingByteOrderMark()
        {
            JsonValue root = Reader().Parse("﻿{}");

            Assert.IsInstanceOf<JsonObject>(root);
        }

        [Test]
        public void Parse_DuplicateKey_ThrowsAtSecondKey()
        {
            CatalogJsonParseException exception =
                Assert.Throws<CatalogJsonParseException>(() => Reader().Parse("{\"a\": 1, \"a\": 2}"));

            Assert.AreEqual(1, exception.Line);
            Assert.AreEqual(10, exception.Column);
        }

        [Test]
        public void Parse_TrailingContent_ThrowsAtGarbage()
        {
            CatalogJsonParseException exception =
                Assert.Throws<CatalogJsonParseException>(() => Reader().Parse("{} x"));

            Assert.AreEqual(1, exception.Line);
            Assert.AreEqual(4, exception.Column);
        }

        [Test]
        public void Parse_Comment_ThrowsAtSlash()
        {
            CatalogJsonParseException exception =
                Assert.Throws<CatalogJsonParseException>(() => Reader().Parse("{\"a\": /* c */ 1}"));

            Assert.AreEqual(1, exception.Line);
            Assert.AreEqual(7, exception.Column);
        }

        [Test]
        public void Parse_SingleQuotedString_Throws()
        {
            CatalogJsonParseException exception =
                Assert.Throws<CatalogJsonParseException>(() => Reader().Parse("{'a': 1}"));

            Assert.AreEqual(1, exception.Line);
            Assert.AreEqual(2, exception.Column);
        }

        [Test]
        public void Parse_UnquotedKey_Throws()
        {
            Assert.Throws<CatalogJsonParseException>(() => Reader().Parse("{a: 1}"));
        }

        [Test]
        public void Parse_NaN_Throws()
        {
            Assert.Throws<CatalogJsonParseException>(() => Reader().Parse("NaN"));
        }

        [Test]
        public void Parse_Infinity_Throws()
        {
            Assert.Throws<CatalogJsonParseException>(() => Reader().Parse("Infinity"));
        }

        [Test]
        public void Parse_LeadingZero_Throws()
        {
            Assert.Throws<CatalogJsonParseException>(() => Reader().Parse("01"));
        }

        [Test]
        public void Parse_LeadingPlus_Throws()
        {
            Assert.Throws<CatalogJsonParseException>(() => Reader().Parse("+1"));
        }

        [Test]
        public void Parse_TrailingCommaInObject_Throws()
        {
            Assert.Throws<CatalogJsonParseException>(() => Reader().Parse("{\"a\": 1,}"));
        }

        [Test]
        public void Parse_TrailingCommaInArray_Throws()
        {
            Assert.Throws<CatalogJsonParseException>(() => Reader().Parse("[1, 2,]"));
        }

        [Test]
        public void Parse_EmptyInput_Throws()
        {
            Assert.Throws<CatalogJsonParseException>(() => Reader().Parse(string.Empty));
        }

        [Test]
        public void Parse_UnterminatedString_Throws()
        {
            Assert.Throws<CatalogJsonParseException>(() => Reader().Parse("\"abc"));
        }
    }
}
