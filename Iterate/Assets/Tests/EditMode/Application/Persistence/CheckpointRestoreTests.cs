using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Persistence;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Persistence.Tests
{
    /// <summary>
    /// Tests <see cref="CheckpointRestore"/>: identities resolved against the live catalog, the Session
    /// rebuilt through its own validate-then-commit entry, and the recovery destination derived from the
    /// Process's authored role rather than from anything that merely correlates with it. Every rejection
    /// returns neither a Session nor a destination — a checkpoint the build cannot honour must not
    /// produce a half-answer a caller could act on.
    /// </summary>
    public sealed class CheckpointRestoreTests
    {
        [Test]
        public void Restore_OfACapturedSession_ReproducesItsSnapshot()
        {
            ContentCatalog catalog = PersistenceFixtures.Catalog();
            SessionState session = Enriched(catalog);
            SessionSnapshot expected = session.Snapshot();

            CheckpointRestoreResult result = CheckpointRestore.Restore(Capture(session), catalog);

            Assert.IsTrue(result.Succeeded, result.Detail);
            AssertSnapshotsEqual(expected, result.Session.Snapshot());
        }

        [Test]
        public void Restore_ReproducesAnAttachedPatchOnItsHost()
        {
            ContentCatalog catalog = PersistenceFixtures.Catalog();
            SessionState session = PersistenceFixtures.Session(catalog);
            InstanceID host = session.Repository.Entries[0].Item.InstanceID;
            InstanceID patch = session.InstanceIDs.Next();
            Attach(session, host, 2, patch);

            CheckpointRestoreResult result = CheckpointRestore.Restore(Capture(session), catalog);

            Assert.IsTrue(result.Succeeded, result.Detail);
            result.Session.Repository.TryGet(host, out RepositoryEntry entry);
            Assert.AreEqual(1, entry.Item.Instruction.AttachedPatches.Count);
            Assert.AreEqual(2, entry.Item.Instruction.AttachedPatches[0].Socket);
            Assert.AreEqual(patch, entry.Item.Instruction.AttachedPatches[0].Patch.InstanceID);
        }

        [Test]
        public void Restore_CarriesTheProcessAndRouteIntoTheDestination()
        {
            ContentCatalog catalog = PersistenceFixtures.Catalog();
            CheckpointState state = Capture(PersistenceFixtures.Session(catalog)) with
            {
                Route = new RouteID(PersistenceFixtures.Route)
            };

            CheckpointDestination destination = CheckpointRestore.Restore(state, catalog).Destination;

            Assert.AreEqual(new ProcessID(PersistenceFixtures.Process), destination.Process);
            Assert.AreEqual(new RouteID(PersistenceFixtures.Route), destination.Route);
        }

        [Test]
        public void Restore_TutorialRolesRecoverByScriptedRestart(
            [Values(ProcessRole.Tutorial1, ProcessRole.Tutorial2)] ProcessRole role
        )
        {
            Assert.AreEqual(CheckpointRecovery.ScriptedRestart, RecoveryFor(role));
        }

        [Test]
        public void Restore_BranchRolesRecoverByBranchConfiguration(
            [Values(ProcessRole.RouteProcess3, ProcessRole.CriticalProcess)] ProcessRole role
        )
        {
            Assert.AreEqual(CheckpointRecovery.BranchConfiguration, RecoveryFor(role));
        }

        [Test]
        public void Restore_AnUnrecognisedRole_IsRejected()
        {
            // The destination is keyed on the authored role, so a role this build does not know is a
            // refusal rather than a guess at which recovery the player should get.
            ContentCatalog catalog = PersistenceFixtures.Catalog((ProcessRole)99);

            AssertRejected(
                Capture(PersistenceFixtures.Session(catalog)),
                catalog,
                CheckpointRestoreRejection.UnknownProcessRole
            );
        }

        [Test]
        public void Restore_AWrongCatalogRevision_IsRejected()
        {
            ContentCatalog catalog = PersistenceFixtures.Catalog();

            AssertRejected(
                Capture(PersistenceFixtures.Session(catalog)) with { CatalogRevision = "0.0.0" },
                catalog,
                CheckpointRestoreRejection.CatalogRevisionMismatch
            );
        }

        [Test]
        public void Restore_AWrongRandomServiceRevision_IsRejected()
        {
            // A deterministic retry that drew differently would not be the same retry, so the PRNG
            // revision is checked as strictly as the catalog's.
            ContentCatalog catalog = PersistenceFixtures.Catalog();

            AssertRejected(
                Capture(PersistenceFixtures.Session(catalog)) with { RandomServiceRevision = "iterate-rng-0" },
                catalog,
                CheckpointRestoreRejection.RandomServiceRevisionMismatch
            );
        }

        [Test]
        public void Restore_AnUnknownProcess_IsRejected()
        {
            ContentCatalog catalog = PersistenceFixtures.Catalog();

            AssertRejected(
                Capture(PersistenceFixtures.Session(catalog)) with { Process = new ProcessID("WB-PROC-999") },
                catalog,
                CheckpointRestoreRejection.UnknownProcess
            );
        }

        [Test]
        public void Restore_AnUnknownRoute_IsRejected()
        {
            ContentCatalog catalog = PersistenceFixtures.Catalog();

            AssertRejected(
                Capture(PersistenceFixtures.Session(catalog)) with { Route = new RouteID("WB-ROUTE-999") },
                catalog,
                CheckpointRestoreRejection.UnknownRoute
            );
        }

        [Test]
        public void Restore_AnUnknownRepositoryDefinition_IsRejected()
        {
            AssertUnknownDefinition(state => state with
            {
                Repository = Replace(state.Repository, 0, state.Repository[0] with { Definition = "WB-INS-999" })
            });
        }

        [Test]
        public void Restore_AnUnknownAttachmentDefinition_IsRejected()
        {
            AssertUnknownDefinition(state => state with
            {
                Repository = Replace(state.Repository, 0, state.Repository[0] with
                {
                    Attachments = new List<CheckpointAttachment>
                    {
                        new(1, new InstanceID(9), "WB-PAT-999")
                    }
                })
            });
        }

        [Test]
        public void Restore_AnUnknownStarterDefinition_IsRejected()
        {
            AssertUnknownDefinition(state => state with
            {
                Economy = state.Economy with
                {
                    Starter = state.Economy.Starter with { Definition = "WB-DEP-999" }
                }
            });
        }

        [Test]
        public void Restore_AnUnknownDependencyDefinition_IsRejected()
        {
            AssertUnknownDefinition(state => state with
            {
                Economy = state.Economy with
                {
                    Dependencies = new List<CheckpointDependency>
                    {
                        new(new InstanceID(9), "WB-DEP-999", 3, DependencyOrigin.Purchase)
                    }
                }
            });
        }

        [Test]
        public void Restore_AnUnknownUtilityDefinition_IsRejected()
        {
            AssertUnknownDefinition(state => state with
            {
                Economy = state.Economy with
                {
                    Utilities = new List<CheckpointUtility>
                    {
                        new(new InstanceID(9), "WB-UTL-999", new ProcessID(PersistenceFixtures.Process), "OFF-901")
                    }
                }
            });
        }

        [Test]
        public void Restore_AnUnknownGrantDefinition_IsRejected()
        {
            AssertUnknownDefinition(state => state with
            {
                Economy = state.Economy with
                {
                    Grants = new List<CheckpointGrant>
                    {
                        new(new InstanceID(9), "WB-PAT-999", false, "reward")
                    }
                }
            });
        }

        [Test]
        public void Restore_ADependencyPositionNamingAnInstruction_IsACategoryMismatch()
        {
            // The ID resolves — just not as a Dependency. Saying so beats claiming the content does not
            // exist, which is what a bare unknown-definition rejection would report.
            ContentCatalog catalog = PersistenceFixtures.Catalog();
            CheckpointState state = Capture(PersistenceFixtures.Session(catalog)) with
            {
                Economy = Capture(PersistenceFixtures.Session(catalog)).Economy with
                {
                    Dependencies = new List<CheckpointDependency>
                    {
                        new(new InstanceID(9), PersistenceFixtures.Instruction, 3, DependencyOrigin.Purchase)
                    }
                }
            };

            AssertRejected(state, catalog, CheckpointRestoreRejection.DefinitionCategoryMismatch);
        }

        [Test]
        public void Restore_ARepositoryPositionNamingADependency_IsACategoryMismatch()
        {
            ContentCatalog catalog = PersistenceFixtures.Catalog();
            CheckpointState captured = Capture(PersistenceFixtures.Session(catalog));
            CheckpointState state = captured with
            {
                Repository = Replace(
                    captured.Repository,
                    0,
                    captured.Repository[0] with { Definition = PersistenceFixtures.StarterDependency }
                )
            };

            AssertRejected(state, catalog, CheckpointRestoreRejection.DefinitionCategoryMismatch);
        }

        [Test]
        public void Restore_ADuplicatedInstance_SurfacesTheSessionsOwnRejection()
        {
            // The cross-holder rules belong to SessionState.Restore, so the checkpoint layer reports
            // which one failed rather than re-implementing it.
            ContentCatalog catalog = PersistenceFixtures.Catalog();
            CheckpointState captured = Capture(PersistenceFixtures.Session(catalog));
            CheckpointState state = captured with
            {
                Repository = Replace(
                    captured.Repository,
                    1,
                    captured.Repository[1] with { Instance = captured.Repository[0].Instance }
                )
            };

            CheckpointRestoreResult result = CheckpointRestore.Restore(state, catalog);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(CheckpointRestoreRejection.SessionRejected, result.Rejection);
            Assert.AreEqual(SessionRestoreRejection.DuplicateInstance, result.SessionRejection);
            Assert.IsNull(result.Session);
            Assert.IsNull(result.Destination);
        }

        [Test]
        public void Restore_ASuccessfulRestore_ReportsNoRejection()
        {
            ContentCatalog catalog = PersistenceFixtures.Catalog();

            CheckpointRestoreResult result = CheckpointRestore.Restore(
                Capture(PersistenceFixtures.Session(catalog)),
                catalog
            );

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(CheckpointRestoreRejection.None, result.Rejection);
            Assert.AreEqual(SessionRestoreRejection.None, result.SessionRejection);
            Assert.IsNull(result.Detail);
        }

        /// <summary>
        /// The recovery a Process of the given role restores into.
        /// </summary>
        /// <param name="role">The role the configuration declares.</param>
        /// <returns>The recovery.</returns>
        private static CheckpointRecovery RecoveryFor(ProcessRole role)
        {
            ContentCatalog catalog = PersistenceFixtures.Catalog(role);
            CheckpointRestoreResult result = CheckpointRestore.Restore(
                Capture(PersistenceFixtures.Session(catalog)),
                catalog
            );

            Assert.IsTrue(result.Succeeded, result.Detail);
            return result.Destination.Recovery;
        }

        /// <summary>
        /// Asserts a mutation of a captured checkpoint is rejected as an unknown definition.
        /// </summary>
        /// <param name="mutate">How to break the checkpoint.</param>
        private static void AssertUnknownDefinition(Func<CheckpointState, CheckpointState> mutate)
        {
            ContentCatalog catalog = PersistenceFixtures.Catalog();

            AssertRejected(
                mutate(Capture(PersistenceFixtures.Session(catalog))),
                catalog,
                CheckpointRestoreRejection.UnknownDefinition
            );
        }

        /// <summary>
        /// Asserts restoring a checkpoint fails with the expected rejection and yields nothing.
        /// </summary>
        /// <param name="state">The checkpoint to restore.</param>
        /// <param name="catalog">The catalog to restore against.</param>
        /// <param name="expected">The rejection expected.</param>
        private static void AssertRejected(
            CheckpointState state,
            ContentCatalog catalog,
            CheckpointRestoreRejection expected
        )
        {
            CheckpointRestoreResult result = CheckpointRestore.Restore(state, catalog);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(expected, result.Rejection);
            Assert.IsNull(result.Session);
            Assert.IsNull(result.Destination, "a rejected restore names no destination.");
            Assert.IsNotNull(result.Detail, "a rejection says what was wrong.");
        }

        /// <summary>
        /// Captures a Session at the fixture Process with no route and no failures.
        /// </summary>
        /// <param name="session">The Session to capture.</param>
        /// <returns>The checkpoint.</returns>
        private static CheckpointState Capture(SessionState session)
        {
            CheckpointCaptureResult result = CheckpointCapture.Capture(
                session,
                new ProcessID(PersistenceFixtures.Process),
                null,
                0
            );

            Assert.IsTrue(result.Succeeded, "the fixture Session must capture.");
            return result.State;
        }

        /// <summary>
        /// A Session carrying a purchase, an installed Dependency, a commitment, a grant and a confirmed
        /// Branch, so a restore has something to lose.
        /// </summary>
        /// <param name="catalog">The catalog to seed from.</param>
        /// <returns>The Session.</returns>
        private static SessionState Enriched(ContentCatalog catalog)
        {
            SessionState session = PersistenceFixtures.Session(catalog);
            session.Repository.Acquire(
                PersistenceFixtures.InstructionOf(PersistenceFixtures.SecondInstruction),
                AcquisitionOrigin.Purchase
            );
            session.Economy.Dependencies.Install(
                PersistenceFixtures.DependencyOf(PersistenceFixtures.CostlyDependency, 1),
                session.InstanceIDs.Next(),
                3,
                DependencyOrigin.Purchase
            );
            session.Economy.CommitUtility(
                PersistenceFixtures.UtilityOf(PersistenceFixtures.Utility),
                new ProcessID(PersistenceFixtures.Process),
                "OFF-901"
            );
            session.Economy.Grant(PersistenceFixtures.PatchOf(PersistenceFixtures.Patch), true, "reward");
            session.Economy.NextTransactionIdentity(TransactionKind.Purchase);
            session.Ordinals.Next(ShopState.Purpose, "WB-SHOP-004");
            session.ActiveBranch.BeginConfiguration(
                new BranchConstraints(
                    9,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>()
                ),
                session.Repository.Snapshot()
            );
            session.ActiveBranch.Confirm();
            return session;
        }

        /// <summary>
        /// Attaches a Patch instance to a held Instruction through the Repository's replacement path.
        /// </summary>
        /// <param name="session">The Session holding the host.</param>
        /// <param name="host">The Instruction instance to attach to.</param>
        /// <param name="socket">The socket to fill.</param>
        /// <param name="patch">The Patch instance's identity.</param>
        private static void Attach(
            SessionState session,
            InstanceID host,
            int socket,
            InstanceID patch
        )
        {
            session.Repository.TryGet(host, out RepositoryEntry entry);
            InstructionInstance derived = entry.Item.Instruction.WithAttachment(new PatchAttachment(
                socket,
                new PatchInstance(patch, PersistenceFixtures.PatchOf(PersistenceFixtures.Patch))
            ));

            session.Repository.Replace(host, entry.Item with { Instruction = derived });
        }

        /// <summary>
        /// Copies a list with one element replaced.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The list to copy.</param>
        /// <param name="index">The index to replace.</param>
        /// <param name="value">The replacement.</param>
        /// <returns>The copy.</returns>
        private static IReadOnlyList<T> Replace<T>(IReadOnlyList<T> source, int index, T value)
        {
            List<T> copy = new(source);
            copy[index] = value;
            return copy;
        }

        /// <summary>
        /// Asserts two confirmed Branches hold the same instances. Compared element-wise rather than by
        /// record equality: <see cref="ActiveBranchConfiguration"/> is a record over a list, so its
        /// generated equality is reference equality on that list — and a checkpoint round trip rebuilds
        /// the list, which is exactly the case this assertion exists to check.
        /// </summary>
        /// <param name="expected">The Branch captured.</param>
        /// <param name="actual">The Branch restored.</param>
        private static void AssertBranchesEqual(ActiveBranchConfiguration expected, ActiveBranchConfiguration actual)
        {
            if (expected == null)
            {
                Assert.IsNull(actual);
                return;
            }

            Assert.IsNotNull(actual);
            CollectionAssert.AreEqual(expected.Eligible, actual.Eligible);
        }

        /// <summary>
        /// Asserts two Session snapshots hold the same values, comparing every list element-wise.
        /// </summary>
        /// <param name="expected">The snapshot captured.</param>
        /// <param name="actual">The snapshot restored.</param>
        private static void AssertSnapshotsEqual(SessionSnapshot expected, SessionSnapshot actual)
        {
            Assert.AreEqual(expected.SystemIdentity, actual.SystemIdentity);
            Assert.AreEqual(expected.SessionIdentity, actual.SessionIdentity);
            Assert.AreEqual(expected.SessionSeedIdentity, actual.SessionSeedIdentity);
            Assert.AreEqual(expected.CatalogRevision, actual.CatalogRevision);
            Assert.AreEqual(expected.LastAllocatedInstance, actual.LastAllocatedInstance);
            AssertBranchesEqual(expected.ConfirmedBranch, actual.ConfirmedBranch);
            CollectionAssert.AreEqual(expected.Ordinals, actual.Ordinals);
            CollectionAssert.AreEqual(expected.Repository, actual.Repository);
            Assert.AreEqual(expected.Economy.Tokens, actual.Economy.Tokens);
            Assert.AreEqual(expected.Economy.RAMCapacity, actual.Economy.RAMCapacity);
            Assert.AreEqual(expected.Economy.Starter, actual.Economy.Starter);
            CollectionAssert.AreEqual(expected.Economy.Installed, actual.Economy.Installed);
            CollectionAssert.AreEqual(expected.Economy.Utilities, actual.Economy.Utilities);
            CollectionAssert.AreEqual(expected.Economy.Grants, actual.Economy.Grants);
            CollectionAssert.AreEqual(expected.Economy.TransactionCounters, actual.Economy.TransactionCounters);
        }
    }
}
