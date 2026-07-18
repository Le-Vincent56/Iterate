using NUnit.Framework;
using Iterate.Application.Logging;

namespace Iterate.Application.Logging.Tests
{
    /// <summary>
    /// Tests that each typed <see cref="LogField.Of"/> overload stores its value in the matching
    /// slot with the matching kind, and that the boxed fallback is reserved for the object overload.
    /// </summary>
    public sealed class LogFieldTests
    {
        [Test]
        public void Of_String_StoresStringSlot()
        {
            LogField field = LogField.Of("name", "core");

            Assert.AreEqual(LogFieldKind.String, field.Kind);
            Assert.AreEqual("core", field.StringValue);
        }

        [Test]
        public void Of_Int_StoresInt64Slot()
        {
            LogField field = LogField.Of("count", 3);

            Assert.AreEqual(LogFieldKind.Int64, field.Kind);
            Assert.AreEqual(3L, field.Int64Value);
        }

        [Test]
        public void Of_Long_StoresInt64Slot()
        {
            LogField field = LogField.Of("ticks", 9L);

            Assert.AreEqual(LogFieldKind.Int64, field.Kind);
            Assert.AreEqual(9L, field.Int64Value);
        }

        [Test]
        public void Of_Float_StoresDoubleSlot()
        {
            LogField field = LogField.Of("ratio", 0.5f);

            Assert.AreEqual(LogFieldKind.Double, field.Kind);
            Assert.AreEqual(0.5d, field.DoubleValue, 0.0001d);
        }

        [Test]
        public void Of_Double_StoresDoubleSlot()
        {
            LogField field = LogField.Of("seconds", 1.25d);

            Assert.AreEqual(LogFieldKind.Double, field.Kind);
            Assert.AreEqual(1.25d, field.DoubleValue, 0.0001d);
        }

        [Test]
        public void Of_Bool_StoresInt64SlotAsBoolKind()
        {
            LogField field = LogField.Of("enabled", true);

            Assert.AreEqual(LogFieldKind.Bool, field.Kind);
            Assert.AreEqual(1L, field.Int64Value);
        }

        [Test]
        public void Of_TypedOverloads_DoNotBox()
        {
            LogField field = LogField.Of("count", 3);

            Assert.IsNull(field.BoxedValue);
        }

        [Test]
        public void Of_Object_StoresBoxedSlot()
        {
            object payload = new();

            LogField field = LogField.Of("payload", payload);

            Assert.AreEqual(LogFieldKind.Boxed, field.Kind);
            Assert.AreSame(payload, field.BoxedValue);
        }
    }
}
