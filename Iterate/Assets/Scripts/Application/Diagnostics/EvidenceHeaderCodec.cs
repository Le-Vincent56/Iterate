using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Application.Diagnostics
{
    /// <summary>
    /// Reads and writes an <see cref="ExecutionEvidenceHeader"/>: the identities an execution is stamped
    /// with, the revisions it ran against, the instances that were in play, and the registers it opened
    /// on. The revision stamps are what make a cross-revision comparison able to say *why* two runs
    /// differ rather than only that they do.
    /// </summary>
    internal static class EvidenceHeaderCodec
    {
        /// <summary>
        /// Writes a header as an object.
        /// </summary>
        /// <param name="header">The header to write.</param>
        /// <returns>The object.</returns>
        internal static JsonObject ToJson(ExecutionEvidenceHeader header)
        {
            List<JsonValue> stamps = new(header.RevisionStamps.Count);
            for (int index = 0; index < header.RevisionStamps.Count; index++)
            {
                RevisionStamp stamp = header.RevisionStamps[index];
                stamps.Add(new JsonObjectBuilder()
                    .Add("name", stamp.Name)
                    .Add("revision", stamp.Revision)
                    .Build());
            }

            return new JsonObjectBuilder()
                .Add("executionIdentity", header.ExecutionIdentity)
                .Add("compilationIdentity", header.CompilationIdentity)
                .Add("compiledSourceRevision", header.CompiledSourceRevision)
                .Add("processIdentity", header.ProcessIdentity)
                .Add("coreIdentity", header.CoreIdentity)
                .Add("processRuleConfigurationIdentity", header.ProcessRuleConfigurationIdentity)
                .Add("sessionSeedIdentity", header.SessionSeedIdentity)
                .Add("revisionStamps", JsonValues.Array(stamps))
                .Add("activeDirectiveInstances", ExecutionRecordCodec.Instances(header.ActiveDirectiveInstances))
                .Add("installedDependencyInstances", ExecutionRecordCodec.Instances(header.InstalledDependencyInstances))
                .Add("relevantPatchInstances", ExecutionRecordCodec.Instances(header.RelevantPatchInstances))
                .Add("initialState", new JsonObjectBuilder()
                    .Add("value", header.InitialState.InitialValue.Value)
                    .Add("signal", header.InitialState.InitialSignal.Value)
                    .Add("score", header.InitialState.InitialScore.Value)
                    .Build())
                .Build();
        }

        /// <summary>
        /// Reads a header from its reader, recording any defect on it rather than throwing.
        /// </summary>
        /// <param name="reader">The header's reader.</param>
        /// <returns>The header, or null when a defect was recorded.</returns>
        internal static ExecutionEvidenceHeader FromJson(JsonMemberReader reader)
        {
            string executionIdentity = reader.String("executionIdentity");
            string compilationIdentity = reader.String("compilationIdentity");
            string compiledSourceRevision = reader.String("compiledSourceRevision");
            string processIdentity = reader.String("processIdentity");
            string coreIdentity = reader.String("coreIdentity");
            string processRuleConfigurationIdentity = reader.String("processRuleConfigurationIdentity");
            string sessionSeedIdentity = reader.String("sessionSeedIdentity");
            List<RevisionStamp> stamps = ReadStamps(reader);
            List<InstanceID> directives = ExecutionRecordCodec.ReadInstances(reader, "activeDirectiveInstances");
            List<InstanceID> dependencies = ExecutionRecordCodec.ReadInstances(reader, "installedDependencyInstances");
            List<InstanceID> patches = ExecutionRecordCodec.ReadInstances(reader, "relevantPatchInstances");

            JsonMemberReader initial = reader.Child("initialState");
            int value = initial.Int("value");
            int signal = initial.Int("signal");
            int score = initial.Int("score");

            if (reader.Failed)
                return null;

            return new ExecutionEvidenceHeader(
                executionIdentity,
                compilationIdentity,
                compiledSourceRevision,
                processIdentity,
                coreIdentity,
                processRuleConfigurationIdentity,
                sessionSeedIdentity,
                stamps,
                directives,
                dependencies,
                patches,
                new InitialExecutionState(new ValueAmount(value), new SignalValue(signal), new ScoreValue(score))
            );
        }

        /// <summary>
        /// Reads the revision stamps.
        /// </summary>
        /// <param name="reader">The header's reader.</param>
        /// <returns>The stamps, or an empty list when a defect was recorded.</returns>
        private static List<RevisionStamp> ReadStamps(JsonMemberReader reader)
        {
            List<RevisionStamp> stamps = new();
            JsonArray items = reader.Array("revisionStamps");
            if (items == null)
                return stamps;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = ExecutionRecordCodec.Element(reader, "revisionStamps", items, index);
                if (element == null)
                    return stamps;

                string name = element.String("name");
                string revision = element.String("revision");
                if (ExecutionRecordCodec.Propagate(reader, "revisionStamps", element))
                    return stamps;

                stamps.Add(new RevisionStamp(name, revision));
            }

            return stamps;
        }
    }
}