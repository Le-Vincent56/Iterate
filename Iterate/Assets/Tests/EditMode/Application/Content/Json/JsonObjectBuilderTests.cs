using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content.Json;

namespace Iterate.Application.Content.Json.Tests
{
    /// <summary>
    /// Tests the writing-side construction helpers <see cref="JsonObjectBuilder"/> and
    /// <see cref="JsonValues"/>: insertion order becomes <see cref="JsonObject.Keys"/> order, a null
    /// string becomes a JSON null, a duplicate key is rejected, and every node is built at line and
    /// column zero because the writing side has no source positions to report.
    /// </summary>
    public sealed class JsonObjectBuilderTests
    {
        [Test]
        public void Build_EmptyBuilder_YieldsNoKeys()
        {
            JsonObject built = new JsonObjectBuilder().Build();

            Assert.AreEqual(0, built.Keys.Count);
            Assert.IsFalse(built.TryGet("anything", out JsonValue missing));
            Assert.IsNull(missing);
        }

        [Test]
        public void Add_PreservesInsertionOrderRegardlessOfAlphabeticalOrder()
        {
            JsonObject built = new JsonObjectBuilder()
                .Add("zebra", 1L)
                .Add("apple", 2L)
                .Add("mango", 3L)
                .Build();

            Assert.AreEqual(3, built.Keys.Count);
            Assert.AreEqual("zebra", built.Keys[0]);
            Assert.AreEqual("apple", built.Keys[1]);
            Assert.AreEqual("mango", built.Keys[2]);
        }

        [Test]
        public void Add_DuplicateKey_Throws()
        {
            JsonObjectBuilder builder = new JsonObjectBuilder().Add("name", "first");

            Assert.Throws<ArgumentException>(() => builder.Add("name", "second"));
        }

        [Test]
        public void Add_NullString_BecomesJsonNull()
        {
            JsonObject built = new JsonObjectBuilder().Add("route", (string)null).Build();

            Assert.IsTrue(built.TryGet("route", out JsonValue value));
            Assert.IsInstanceOf<JsonNull>(value);
        }

        [Test]
        public void Add_String_WrapsAsJsonString()
        {
            JsonObject built = new JsonObjectBuilder().Add("route", "WB-ROUTE-001").Build();

            Assert.IsTrue(built.TryGet("route", out JsonValue value));
            Assert.AreEqual("WB-ROUTE-001", ((JsonString)value).Value);
        }

        [Test]
        public void Add_Long_WrapsAsIntegerNumber()
        {
            JsonObject built = new JsonObjectBuilder().Add("count", 7L).Build();

            Assert.IsTrue(built.TryGet("count", out JsonValue value));
            JsonNumber number = (JsonNumber)value;
            Assert.IsTrue(number.IsInteger);
            Assert.AreEqual(7L, number.IntegerValue);
        }

        [Test]
        public void Add_Bool_WrapsAsJsonBool()
        {
            JsonObject built = new JsonObjectBuilder().Add("onboardingComplete", true).Build();

            Assert.IsTrue(built.TryGet("onboardingComplete", out JsonValue value));
            Assert.IsTrue(((JsonBool)value).Value);
        }

        [Test]
        public void AddNull_AddsAJsonNullMember()
        {
            JsonObject built = new JsonObjectBuilder().AddNull("branch").Build();

            Assert.IsTrue(built.TryGet("branch", out JsonValue value));
            Assert.IsInstanceOf<JsonNull>(value);
        }

        [Test]
        public void Add_ReturnsTheSameBuilderForChaining()
        {
            JsonObjectBuilder builder = new();

            Assert.AreSame(builder, builder.Add("a", 1L));
        }

        [Test]
        public void Build_ReportsZeroLineAndColumn()
        {
            JsonObject built = new JsonObjectBuilder().Add("a", 1L).Build();

            Assert.AreEqual(0, built.Line);
            Assert.AreEqual(0, built.Column);
            Assert.IsTrue(built.TryGet("a", out JsonValue value));
            Assert.AreEqual(0, value.Line);
            Assert.AreEqual(0, value.Column);
        }

        [Test]
        public void JsonValues_String_RejectsNull()
        {
            Assert.Throws<ArgumentException>(() => JsonValues.String(null));
        }

        [Test]
        public void JsonValues_StringOrNull_MapsNullToJsonNullAndTextToJsonString()
        {
            Assert.IsInstanceOf<JsonNull>(JsonValues.StringOrNull(null));
            Assert.AreEqual("text", ((JsonString)JsonValues.StringOrNull("text")).Value);
        }

        [Test]
        public void JsonValues_Double_CarriesTheNonIntegerForm()
        {
            JsonNumber number = JsonValues.Double(1.5d);

            Assert.IsFalse(number.IsInteger);
            Assert.AreEqual(1.5d, number.DoubleValue);
        }

        [Test]
        public void JsonValues_Array_PreservesItemOrder()
        {
            List<JsonValue> items = new() { JsonValues.Integer(3L), JsonValues.Integer(1L) };

            JsonArray array = JsonValues.Array(items);

            Assert.AreEqual(2, array.Items.Count);
            Assert.AreEqual(3L, ((JsonNumber)array.Items[0]).IntegerValue);
            Assert.AreEqual(1L, ((JsonNumber)array.Items[1]).IntegerValue);
        }
    }
}
