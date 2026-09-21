using System;
using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Application.Persistence;
using Iterate.Domain.Determinism;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Persistence.Tests
{
    /// <summary>
    /// Hand-built <see cref="CheckpointState"/> values for the codec suites. Two shapes matter: a full
    /// checkpoint where every list carries something, a route is taken and a Branch is confirmed, and a
    /// tutorial checkpoint where there is no route, no Branch and nothing bought — the state the very
    /// first Process saves. Together they exercise both sides of every nullable and every empty list.
    /// </summary>
    public static class CheckpointFixtures
    {
        /// <summary>
        /// The catalog revision the fixtures claim to have been written against.
        /// </summary>
        public const string CatalogRevision = "fixture-0.0.1";

        /// <summary>
        /// A checkpoint with every list populated, a route taken, a Branch confirmed and failures
        /// recorded — the widest shape the codec has to carry.
        /// </summary>
        /// <returns>The checkpoint state.</returns>
        public static CheckpointState Full()
        {
            List<CheckpointAttachment> attachments = new()
            {
                new CheckpointAttachment(1, new InstanceID(8), "WB-PAT-001"),
                new CheckpointAttachment(2, new InstanceID(9), "WB-PAT-001")
            };

            List<CheckpointRepositoryItem> repository = new()
            {
                new CheckpointRepositoryItem(
                    new InstanceID(1),
                    "WB-INS-002",
                    AcquisitionOrigin.Starter,
                    true,
                    1,
                    attachments
                ),
                new CheckpointRepositoryItem(
                    new InstanceID(3),
                    "WB-STR-001",
                    AcquisitionOrigin.Purchase,
                    false,
                    1,
                    Array.Empty<CheckpointAttachment>()
                )
            };

            CheckpointEconomy economy = new(
                7,
                5,
                new CheckpointStarter(new InstanceID(4), "WB-DEP-001"),
                new List<CheckpointDependency>
                {
                    new(new InstanceID(10), "WB-DEP-002", 3, DependencyOrigin.Purchase)
                },
                new List<CheckpointUtility>
                {
                    new(new InstanceID(11), "WB-UTL-201", new ProcessID("WB-PROC-004"), "OFF-001")
                },
                new List<CheckpointGrant>
                {
                    new(new InstanceID(12), "WB-PAT-001", true, "reward")
                },
                new List<TransactionCounter>
                {
                    new(TransactionKind.Purchase, 2),
                    new(TransactionKind.Reroll, 1)
                }
            );

            return new CheckpointState(
                CatalogRevision,
                DeterminismService.RevisionIdentity,
                "WB-SYS-001",
                "session-1",
                "seed-1",
                new ProcessID("WB-PROC-004"),
                new RouteID("WB-ROUTE-002"),
                2,
                12,
                new List<OrdinalCounter> { new("Shop offer replacement", "WB-SHOP-004", 2) },
                repository,
                new List<InstanceID> { new(1), new(3) },
                economy
            );
        }

        /// <summary>
        /// A checkpoint as the first tutorial Process saves one: no route, no confirmed Branch, no
        /// purchases and no failures yet.
        /// </summary>
        /// <returns>The checkpoint state.</returns>
        public static CheckpointState Tutorial()
        {
            CheckpointEconomy economy = new(
                0,
                4,
                new CheckpointStarter(new InstanceID(4), "WB-DEP-001"),
                Array.Empty<CheckpointDependency>(),
                Array.Empty<CheckpointUtility>(),
                Array.Empty<CheckpointGrant>(),
                Array.Empty<TransactionCounter>()
            );

            return new CheckpointState(
                CatalogRevision,
                DeterminismService.RevisionIdentity,
                "WB-SYS-001",
                "session-1",
                "seed-1",
                new ProcessID("WB-PROC-001"),
                null,
                0,
                4,
                Array.Empty<OrdinalCounter>(),
                new List<CheckpointRepositoryItem>
                {
                    new(
                        new InstanceID(1),
                        "WB-INS-002",
                        AcquisitionOrigin.Starter,
                        true,
                        1,
                        Array.Empty<CheckpointAttachment>()
                    )
                },
                null,
                economy
            );
        }

        /// <summary>
        /// The document text a checkpoint is written as, envelope included.
        /// </summary>
        /// <param name="state">The checkpoint to write.</param>
        /// <returns>The document text.</returns>
        public static string Json(CheckpointState state)
        {
            return VersionedDocument.Write(SaveFiles.FormatVersion, CheckpointStateCodec.ToJson(state));
        }
    }
}
