using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.Diagnostics;

namespace Iterate.Application.Diagnostics.Tests
{
    /// <summary>
    /// Tests <see cref="ExecutionRecordDocument"/>, the envelope a generated execution record is written
    /// and read through. Four outcomes have to stay distinguishable: text that is not a versioned
    /// document at all, a document from a build that wrote a later format, a document whose record
    /// cannot be rebuilt, and one that loads. A baseline comparison that could not tell those apart
    /// would report a divergence for a file it simply could not read.
    /// </summary>
    public sealed class ExecutionRecordDocumentTests
    {
        [Test]
        public void Write_StampsTheRecordCodecsFormatVersionFirst()
        {
            string text = ExecutionRecordDocument.Write(RecordFixtures.Minimal());

            StringAssert.StartsWith("{\n  \"formatVersion\": 1,\n", text);
        }

        [Test]
        public void Write_EndsWithASingleNewline()
        {
            string text = ExecutionRecordDocument.Write(RecordFixtures.Minimal());

            StringAssert.EndsWith("}\n", text);
            Assert.AreNotEqual("\n\n", text.Substring(text.Length - 2));
        }

        [Test]
        public void WriteThenRead_LoadsAnEqualRecord()
        {
            ExecutionRecordReadResult result = ExecutionRecordDocument.Read(
                ExecutionRecordDocument.Write(RecordFixtures.WithAddedExecution())
            );

            Assert.AreEqual(ExecutionRecordReadOutcome.Loaded, result.Outcome);
            Assert.AreEqual(RecordFixtures.WithAddedExecution(), result.Record);
            Assert.AreEqual(1, result.FormatVersion);
            Assert.IsNull(result.Defect);
        }

        [Test]
        public void Write_IsByteIdenticalForTheSameRecord()
        {
            Assert.AreEqual(
                ExecutionRecordDocument.Write(RecordFixtures.WithStructureContext()),
                ExecutionRecordDocument.Write(RecordFixtures.WithStructureContext())
            );
        }

        [Test]
        public void ReadThenWrite_IsByteIdenticalToTheOriginalText()
        {
            // The property a baseline comparison depends on: a record written, read and written again
            // must produce the same bytes, or every refresh would look like a change.
            string first = ExecutionRecordDocument.Write(RecordFixtures.WithDefects());

            ExecutionRecordReadResult read = ExecutionRecordDocument.Read(first);

            Assert.AreEqual(ExecutionRecordReadOutcome.Loaded, read.Outcome, read.Defect);
            Assert.AreEqual(first, ExecutionRecordDocument.Write(read.Record));
        }

        [Test]
        public void Read_OfTextThatIsNotJson_IsMalformed()
        {
            ExecutionRecordReadResult result = ExecutionRecordDocument.Read("not json");

            Assert.AreEqual(ExecutionRecordReadOutcome.Malformed, result.Outcome);
            Assert.IsNull(result.Record);
            Assert.IsNotNull(result.Defect);
        }

        [Test]
        public void Read_OfEmptyText_IsMalformed()
        {
            Assert.AreEqual(ExecutionRecordReadOutcome.Malformed, ExecutionRecordDocument.Read("").Outcome);
        }

        [Test]
        public void Read_OfAFutureVersion_IsUnsupportedAndCarriesTheVersion()
        {
            ExecutionRecordReadResult result = ExecutionRecordDocument.Read("{\n  \"formatVersion\": 2\n}\n");

            Assert.AreEqual(ExecutionRecordReadOutcome.UnsupportedVersion, result.Outcome);
            Assert.AreEqual(2, result.FormatVersion);
            Assert.IsNull(result.Record);
        }

        [Test]
        public void Read_OfAVersionedDocumentThatIsNotARecord_IsDefective()
        {
            // The envelope is intact and the version is right; what is inside cannot be rebuilt. That is
            // a different failure from malformed text and reports differently.
            string text = VersionedDocument.Write(1, new JsonObjectBuilder().Add("unrelated", 1L).Build());

            ExecutionRecordReadResult result = ExecutionRecordDocument.Read(text);

            Assert.AreEqual(ExecutionRecordReadOutcome.Defective, result.Outcome);
            Assert.IsNull(result.Record);
            Assert.IsNotNull(result.Defect);
        }

        [Test]
        public void Read_OfARecordWithAnImpossibleStatusPair_IsDefective()
        {
            string text = ExecutionRecordDocument.Write(RecordFixtures.Aborted());
            string broken = text.Replace("\"completionStatus\": \"SafetyAborted\"", "\"completionStatus\": \"Completed\"");

            ExecutionRecordReadResult result = ExecutionRecordDocument.Read(broken);

            Assert.AreEqual(ExecutionRecordReadOutcome.Defective, result.Outcome);
            Assert.IsNotNull(result.Defect);
        }
    }
}
