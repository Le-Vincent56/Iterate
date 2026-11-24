using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Domain.Determinism;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// Reads and writes <see cref="CheckpointState"/>. Members are written in a fixed order so two equal
    /// checkpoints always produce identical bytes, and every member is required on the way back in: an
    /// absent one is a defect naming it rather than a default, because a checkpoint that silently
    /// defaulted a Token balance or a suffix would restore a Session the player never played.
    ///
    /// Nothing is constructed until the whole document has been read without defect. Three of the value
    /// types here reject their own default, so constructing as we read would turn a missing member into
    /// an exception escaping the load path instead of a defect a caller can report.
    /// </summary>
    public static class CheckpointStateCodec
    {
        private const string Path = "checkpoint";

        /// <summary>
        /// Writes a checkpoint as a payload.
        /// </summary>
        /// <param name="state">The checkpoint to write.</param>
        /// <returns>The payload.</returns>
        public static JsonObject ToJson(CheckpointState state)
        {
            return new JsonObjectBuilder()
                .Add("catalogRevision", state.CatalogRevision)
                .Add("randomServiceRevision", state.RandomServiceRevision)
                .Add("systemIdentity", state.SystemIdentity)
                .Add("sessionIdentity", state.SessionIdentity)
                .Add("sessionSeedIdentity", state.SessionSeedIdentity)
                .Add("process", state.Process.Value)
                .Add("route", state.Route.HasValue ? state.Route.Value.Value : null)
                .Add("consecutiveFailures", state.ConsecutiveFailures)
                .Add("lastAllocatedInstance", state.LastAllocatedInstance)
                .Add("ordinals", Ordinals(state.Ordinals))
                .Add("repository", Repository(state.Repository))
                .Add("branch", Branch(state.Branch))
                .Add("economy", Economy(state.Economy))
                .Build();
        }

        /// <summary>
        /// Reads a checkpoint from a payload.
        /// </summary>
        /// <param name="payload">The payload to read.</param>
        /// <returns>The decoded checkpoint, or a defect naming the member that was wrong.</returns>
        public static CodecResult<CheckpointState> FromJson(JsonObject payload)
        {
            if (payload == null)
                return CodecResult<CheckpointState>.Fail($"{Path}: the payload is absent.");

            JsonMemberReader reader = new(payload, Path);
            string catalogRevision = reader.String("catalogRevision");
            string randomServiceRevision = reader.String("randomServiceRevision");
            string systemIdentity = reader.String("systemIdentity");
            string sessionIdentity = reader.String("sessionIdentity");
            string sessionSeedIdentity = reader.String("sessionSeedIdentity");
            string process = reader.String("process");
            string route = reader.OptionalString("route");
            int consecutiveFailures = reader.Int("consecutiveFailures");
            int lastAllocatedInstance = reader.Int("lastAllocatedInstance");

            List<OrdinalCounter> ordinals = ReadOrdinals(reader);
            List<CheckpointRepositoryItem> repository = ReadRepository(reader);
            List<InstanceID> branch = ReadBranch(reader);
            CheckpointEconomy economy = ReadEconomy(reader);

            if (reader.Failed)
                return CodecResult<CheckpointState>.Fail(reader.Defect);

            return CodecResult<CheckpointState>.Ok(new CheckpointState(
                catalogRevision,
                randomServiceRevision,
                systemIdentity,
                sessionIdentity,
                sessionSeedIdentity,
                new ProcessID(process),
                route == null ? null : new RouteID(route),
                consecutiveFailures,
                lastAllocatedInstance,
                ordinals,
                repository,
                branch,
                economy
            ));
        }

        /// <summary>
        /// Writes the occurrence-ordinal counters.
        /// </summary>
        /// <param name="ordinals">The counters to write.</param>
        /// <returns>The array.</returns>
        private static JsonArray Ordinals(IReadOnlyList<OrdinalCounter> ordinals)
        {
            List<JsonValue> items = new(ordinals.Count);
            for (int index = 0; index < ordinals.Count; index++)
            {
                OrdinalCounter counter = ordinals[index];
                items.Add(new JsonObjectBuilder()
                    .Add("selectionPurpose", counter.SelectionPurpose)
                    .Add("governingContextIdentity", counter.GoverningContextIdentity)
                    .Add("count", counter.Count)
                    .Build());
            }

            return JsonValues.Array(items);
        }

        /// <summary>
        /// Writes the Repository entries and their attachments.
        /// </summary>
        /// <param name="repository">The entries to write.</param>
        /// <returns>The array.</returns>
        private static JsonArray Repository(IReadOnlyList<CheckpointRepositoryItem> repository)
        {
            List<JsonValue> items = new(repository.Count);
            for (int index = 0; index < repository.Count; index++)
            {
                CheckpointRepositoryItem item = repository[index];
                List<JsonValue> attachments = new(item.Attachments.Count);
                for (int socket = 0; socket < item.Attachments.Count; socket++)
                {
                    CheckpointAttachment attachment = item.Attachments[socket];
                    attachments.Add(new JsonObjectBuilder()
                        .Add("socket", attachment.Socket)
                        .Add("instance", attachment.Instance.Value)
                        .Add("definition", attachment.Definition)
                        .Build());
                }

                items.Add(new JsonObjectBuilder()
                    .Add("instance", item.Instance.Value)
                    .Add("definition", item.Definition)
                    .Add("origin", item.Origin.ToString())
                    .Add("starterProtected", item.StarterProtected)
                    .Add("suffix", item.Suffix)
                    .Add("attachments", JsonValues.Array(attachments))
                    .Build());
            }

            return JsonValues.Array(items);
        }

        /// <summary>
        /// Writes the confirmed Branch, or a null literal when there is none.
        /// </summary>
        /// <param name="branch">The Branch instances, or null.</param>
        /// <returns>The array or the null literal.</returns>
        private static JsonValue Branch(IReadOnlyList<InstanceID> branch)
        {
            if (branch == null)
                return JsonValues.Null();

            List<JsonValue> items = new(branch.Count);
            for (int index = 0; index < branch.Count; index++)
            {
                items.Add(JsonValues.Integer(branch[index].Value));
            }

            return JsonValues.Array(items);
        }

        /// <summary>
        /// Writes the economy.
        /// </summary>
        /// <param name="economy">The economy to write.</param>
        /// <returns>The object.</returns>
        private static JsonObject Economy(CheckpointEconomy economy)
        {
            List<JsonValue> dependencies = new(economy.Dependencies.Count);
            for (int index = 0; index < economy.Dependencies.Count; index++)
            {
                CheckpointDependency dependency = economy.Dependencies[index];
                dependencies.Add(new JsonObjectBuilder()
                    .Add("instance", dependency.Instance.Value)
                    .Add("definition", dependency.Definition)
                    .Add("pricePaid", dependency.PricePaid)
                    .Add("origin", dependency.Origin.ToString())
                    .Build());
            }

            List<JsonValue> utilities = new(economy.Utilities.Count);
            for (int index = 0; index < economy.Utilities.Count; index++)
            {
                CheckpointUtility utility = economy.Utilities[index];
                utilities.Add(new JsonObjectBuilder()
                    .Add("instance", utility.Instance.Value)
                    .Add("definition", utility.Definition)
                    .Add("targetProcess", utility.TargetProcess.Value)
                    .Add("offerID", utility.OfferID)
                    .Build());
            }

            List<JsonValue> grants = new(economy.Grants.Count);
            for (int index = 0; index < economy.Grants.Count; index++)
            {
                CheckpointGrant grant = economy.Grants[index];
                grants.Add(new JsonObjectBuilder()
                    .Add("instance", grant.Instance.Value)
                    .Add("definition", grant.Definition)
                    .Add("cached", grant.Cached)
                    .Add("basis", grant.Basis)
                    .Build());
            }

            List<JsonValue> counters = new(economy.TransactionCounters.Count);
            for (int index = 0; index < economy.TransactionCounters.Count; index++)
            {
                TransactionCounter counter = economy.TransactionCounters[index];
                counters.Add(new JsonObjectBuilder()
                    .Add("kind", counter.Kind.ToString())
                    .Add("count", counter.Count)
                    .Build());
            }

            return new JsonObjectBuilder()
                .Add("tokens", economy.Tokens)
                .Add("ramCapacity", economy.RAMCapacity)
                .Add("starter", new JsonObjectBuilder()
                    .Add("instance", economy.Starter.Instance.Value)
                    .Add("definition", economy.Starter.Definition)
                    .Build())
                .Add("dependencies", JsonValues.Array(dependencies))
                .Add("utilities", JsonValues.Array(utilities))
                .Add("grants", JsonValues.Array(grants))
                .Add("transactionCounters", JsonValues.Array(counters))
                .Build();
        }

        /// <summary>
        /// Reads the occurrence-ordinal counters.
        /// </summary>
        /// <param name="reader">The document reader.</param>
        /// <returns>The counters, or an empty list when a defect was recorded.</returns>
        private static List<OrdinalCounter> ReadOrdinals(JsonMemberReader reader)
        {
            List<OrdinalCounter> ordinals = new();
            JsonArray items = reader.Array("ordinals");
            if (items == null)
                return ordinals;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = Element(reader, "ordinals", items, index);
                if (element == null)
                    return ordinals;

                string purpose = element.String("selectionPurpose");
                string context = element.String("governingContextIdentity");
                int count = element.Int("count");
                if (Propagate(reader, "ordinals", element))
                    return ordinals;

                ordinals.Add(new OrdinalCounter(purpose, context, count));
            }

            return ordinals;
        }

        /// <summary>
        /// Reads the Repository entries and their attachments.
        /// </summary>
        /// <param name="reader">The document reader.</param>
        /// <returns>The entries, or an empty list when a defect was recorded.</returns>
        private static List<CheckpointRepositoryItem> ReadRepository(JsonMemberReader reader)
        {
            List<CheckpointRepositoryItem> repository = new();
            JsonArray items = reader.Array("repository");
            if (items == null)
                return repository;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = Element(reader, "repository", items, index);
                if (element == null)
                    return repository;

                int instance = element.Int("instance");
                string definition = element.String("definition");
                AcquisitionOrigin origin = element.Enum<AcquisitionOrigin>("origin");
                bool starterProtected = element.Bool("starterProtected");
                int suffix = element.Int("suffix");
                List<CheckpointAttachment> attachments = ReadAttachments(element);
                if (Propagate(reader, "repository", element))
                    return repository;

                repository.Add(new CheckpointRepositoryItem(
                    new InstanceID(instance),
                    definition,
                    origin,
                    starterProtected,
                    suffix,
                    attachments
                ));
            }

            return repository;
        }

        /// <summary>
        /// Reads one Repository entry's Patch attachments.
        /// </summary>
        /// <param name="item">The entry's reader.</param>
        /// <returns>The attachments, or an empty list when a defect was recorded.</returns>
        private static List<CheckpointAttachment> ReadAttachments(JsonMemberReader item)
        {
            List<CheckpointAttachment> attachments = new();
            JsonArray items = item.Array("attachments");
            if (items == null)
                return attachments;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = Element(item, "attachments", items, index);
                if (element == null)
                    return attachments;

                int socket = element.Int("socket");
                int instance = element.Int("instance");
                string definition = element.String("definition");
                if (Propagate(item, "attachments", element))
                    return attachments;

                attachments.Add(new CheckpointAttachment(socket, new InstanceID(instance), definition));
            }

            return attachments;
        }

        /// <summary>
        /// Reads the confirmed Branch, distinguishing an absent member from a null one.
        /// </summary>
        /// <param name="reader">The document reader.</param>
        /// <returns>The Branch instances, or null when the member is a JSON null.</returns>
        private static List<InstanceID> ReadBranch(JsonMemberReader reader)
        {
            JsonArray items = reader.OptionalArray("branch");
            if (items == null)
                return null;

            List<InstanceID> branch = new(items.Items.Count);
            for (int index = 0; index < items.Items.Count; index++)
            {
                if (items.Items[index] is not JsonNumber number || !number.IsInteger || number.IntegerValue < 1)
                {
                    reader.Fail("branch", "an entry is not an instance identity.");
                    return branch;
                }

                branch.Add(new InstanceID((int)number.IntegerValue));
            }

            return branch;
        }

        /// <summary>
        /// Reads the economy and its four lists.
        /// </summary>
        /// <param name="reader">The document reader.</param>
        /// <returns>The economy, or null when a defect was recorded.</returns>
        private static CheckpointEconomy ReadEconomy(JsonMemberReader reader)
        {
            JsonMemberReader economy = reader.Child("economy");
            int tokens = economy.Int("tokens");
            int ramCapacity = economy.Int("ramCapacity");
            JsonMemberReader starter = economy.Child("starter");
            int starterInstance = starter.Int("instance");
            string starterDefinition = starter.String("definition");

            List<CheckpointDependency> dependencies = ReadDependencies(economy);
            List<CheckpointUtility> utilities = ReadUtilities(economy);
            List<CheckpointGrant> grants = ReadGrants(economy);
            List<TransactionCounter> counters = ReadCounters(economy);

            if (reader.Failed)
                return null;

            return new CheckpointEconomy(
                tokens,
                ramCapacity,
                new CheckpointStarter(new InstanceID(starterInstance), starterDefinition),
                dependencies,
                utilities,
                grants,
                counters
            );
        }

        /// <summary>
        /// Reads the installed Dependencies.
        /// </summary>
        /// <param name="economy">The economy's reader.</param>
        /// <returns>The Dependencies, or an empty list when a defect was recorded.</returns>
        private static List<CheckpointDependency> ReadDependencies(JsonMemberReader economy)
        {
            List<CheckpointDependency> dependencies = new();
            JsonArray items = economy.Array("dependencies");
            if (items == null)
                return dependencies;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = Element(economy, "dependencies", items, index);
                if (element == null)
                    return dependencies;

                int instance = element.Int("instance");
                string definition = element.String("definition");
                int pricePaid = element.Int("pricePaid");
                DependencyOrigin origin = element.Enum<DependencyOrigin>("origin");
                if (Propagate(economy, "dependencies", element))
                    return dependencies;

                dependencies.Add(new CheckpointDependency(
                    new InstanceID(instance),
                    definition,
                    pricePaid,
                    origin
                ));
            }

            return dependencies;
        }

        /// <summary>
        /// Reads the Utility commitments.
        /// </summary>
        /// <param name="economy">The economy's reader.</param>
        /// <returns>The commitments, or an empty list when a defect was recorded.</returns>
        private static List<CheckpointUtility> ReadUtilities(JsonMemberReader economy)
        {
            List<CheckpointUtility> utilities = new();
            JsonArray items = economy.Array("utilities");
            if (items == null)
                return utilities;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = Element(economy, "utilities", items, index);
                if (element == null)
                    return utilities;

                int instance = element.Int("instance");
                string definition = element.String("definition");
                string targetProcess = element.String("targetProcess");
                string offerID = element.String("offerID");
                if (Propagate(economy, "utilities", element))
                    return utilities;

                utilities.Add(new CheckpointUtility(
                    new InstanceID(instance),
                    definition,
                    new ProcessID(targetProcess),
                    offerID
                ));
            }

            return utilities;
        }

        /// <summary>
        /// Reads the outstanding Patch grants.
        /// </summary>
        /// <param name="economy">The economy's reader.</param>
        /// <returns>The grants, or an empty list when a defect was recorded.</returns>
        private static List<CheckpointGrant> ReadGrants(JsonMemberReader economy)
        {
            List<CheckpointGrant> grants = new();
            JsonArray items = economy.Array("grants");
            if (items == null)
                return grants;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = Element(economy, "grants", items, index);
                if (element == null)
                    return grants;

                int instance = element.Int("instance");
                string definition = element.String("definition");
                bool cached = element.Bool("cached");
                string basis = element.String("basis");
                if (Propagate(economy, "grants", element))
                    return grants;

                grants.Add(new CheckpointGrant(new InstanceID(instance), definition, cached, basis));
            }

            return grants;
        }

        /// <summary>
        /// Reads the per-kind transaction counters.
        /// </summary>
        /// <param name="economy">The economy's reader.</param>
        /// <returns>The counters, or an empty list when a defect was recorded.</returns>
        private static List<TransactionCounter> ReadCounters(JsonMemberReader economy)
        {
            List<TransactionCounter> counters = new();
            JsonArray items = economy.Array("transactionCounters");
            if (items == null)
                return counters;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = Element(economy, "transactionCounters", items, index);
                if (element == null)
                    return counters;

                TransactionKind kind = element.Enum<TransactionKind>("kind");
                int count = element.Int("count");
                if (Propagate(economy, "transactionCounters", element))
                    return counters;

                counters.Add(new TransactionCounter(kind, count));
            }

            return counters;
        }

        /// <summary>
        /// Opens a reader over one array element, failing the parent when the element is not an object.
        /// </summary>
        /// <param name="parent">The reader the array came from.</param>
        /// <param name="key">The array's member name.</param>
        /// <param name="items">The array.</param>
        /// <param name="index">The element index.</param>
        /// <returns>The element's reader, or null when the element is not an object.</returns>
        private static JsonMemberReader Element(
            JsonMemberReader parent,
            string key,
            JsonArray items,
            int index
        )
        {
            if (items.Items[index] is JsonObject element)
                return new JsonMemberReader(element, key);

            parent.Fail(key, "an entry is not an object.");
            return null;
        }

        /// <summary>
        /// Carries an element reader's defect up to the reader the entry-point checks.
        /// </summary>
        /// <param name="parent">The reader the array came from.</param>
        /// <param name="key">The array's member name.</param>
        /// <param name="element">The element's reader.</param>
        /// <returns>True when a defect was carried up.</returns>
        private static bool Propagate(JsonMemberReader parent, string key, JsonMemberReader element)
        {
            if (!element.Failed)
                return false;

            parent.Fail(key, element.Defect);
            return true;
        }
    }
}