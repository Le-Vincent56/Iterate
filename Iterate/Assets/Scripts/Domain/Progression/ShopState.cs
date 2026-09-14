using System;
using System.Collections.Generic;
using System.Globalization;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One opened shop: numbered slots over resolved offers, free pinning, escalating rerolls drawn as
    /// Determinism decisions, and purchases routed by content category. A shop is opened for the one
    /// Process it precedes, which is the Process any Utility bought here commits to. Closing it ends
    /// every operation, so a shop cannot be reached again once the player has moved on.
    /// </summary>
    public sealed class ShopState
    {
        /// <summary>
        /// The registered selection purpose every reroll decision declares.
        /// </summary>
        public const string Purpose = "Shop offer replacement";

        /// <summary>
        /// How many times one shop may be rerolled. Three costs are authored (WB-PAR-028..030) and no
        /// fourth exists, so the third reroll is the last.
        /// </summary>
        public const int MaximumRerolls = 3;

        private const string SelectionBoundaryIdentity = "Shop reroll commitment";

        private const string CandidateSourceIdentity = "reroll pool, unseen";

        private const string EligibilityRuleIdentity = "not seen in this shop";

        private const string SnapshotTimingIdentity = "at reroll commitment";

        private const string OrderingRuleIdentity = "canonical content identity";

        private const string TieBreakRuleIdentity = "canonical content identity";

        private readonly DeterminismService _service = new();

        private readonly List<ShopSlot> _slots = new();

        private readonly List<string> _seen = new();

        private readonly List<PinRecord> _pins = new();

        private readonly List<TransactionRecord> _transactions = new();

        private readonly SessionState _session;

        private readonly ContentCatalog _catalog;

        private readonly PoolDefinition _pool;

        /// <summary>
        /// The definition this shop was opened from.
        /// </summary>
        public ShopDefinition Definition { get; }

        /// <summary>
        /// The Process this shop precedes, and which its Utilities commit to.
        /// </summary>
        public ProcessID TargetProcess { get; }

        /// <summary>
        /// Whether the shop still accepts operations.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// The numbered slots, in slot order.
        /// </summary>
        public IReadOnlyList<ShopSlot> Slots => _slots;

        /// <summary>
        /// How many rerolls have been spent here.
        /// </summary>
        public int RerollsUsed { get; private set; }

        /// <summary>
        /// What the next reroll costs, or null when none remains.
        /// </summary>
        public int? NextRerollCost
        {
            get
            {
                if (!Definition.RerollsEnabled || RerollsUsed >= MaximumRerolls)
                    return null;

                ParameterSet parameters = _session.Economy.Parameters;
                switch (RerollsUsed)
                {
                    case 0: return parameters.FirstRerollCost;
                    case 1: return parameters.SecondRerollCost;
                    default: return parameters.ThirdRerollCost;
                }
            }
        }

        /// <summary>
        /// Every content ID this shop has shown, so a reroll never repeats one.
        /// </summary>
        public IReadOnlyList<string> SeenContent => _seen;

        /// <summary>
        /// Every pinning action, in order.
        /// </summary>
        public IReadOnlyList<PinRecord> Pins => _pins;

        /// <summary>
        /// This shop's committed transactions, which are also appended to the Session's records.
        /// </summary>
        public IReadOnlyList<TransactionRecord> Transactions => _transactions;

        private ShopState(
            ShopDefinition definition,
            ContentCatalog catalog,
            SessionState session,
            ProcessID targetProcess,
            PoolDefinition pool
        )
        {
            Definition = definition;
            TargetProcess = targetProcess;
            _catalog = catalog;
            _session = session;
            _pool = pool;
            IsOpen = true;
        }

        /// <summary>
        /// Opens a shop for the Process it precedes, resolving every fixed offer and the reroll pool.
        /// Content the catalog cannot supply refuses the whole shop rather than opening it short.
        /// </summary>
        /// <param name="definition">The shop definition.</param>
        /// <param name="catalog">The frozen catalog.</param>
        /// <param name="session">The Session the shop transacts against.</param>
        /// <param name="targetProcess">The Process this shop precedes.</param>
        /// <returns>The open result.</returns>
        public static ShopOpenResult Open(
            ShopDefinition definition,
            ContentCatalog catalog,
            SessionState session,
            ProcessID targetProcess
        )
        {
            if (definition == null)
                throw new ArgumentException("Opening a shop requires a definition.", nameof(definition));

            PoolDefinition pool = null;
            if (definition.RerollPool.HasValue
                && !catalog.TryGetPool(definition.RerollPool.Value, out pool))
            {
                return new ShopOpenResult(false, ShopRejection.PoolMissing, null);
            }

            ShopState shop = new(definition, catalog, session, targetProcess, pool);
            IReadOnlyList<ShopOffer> offers = definition.FixedOffers;
            for (int i = 0; i < offers.Count; i++)
            {
                if (!shop.TryResolve(offers[i].OfferID, offers[i].Content, offers[i].Price, out ShopSlotOffer resolved))
                    return new ShopOpenResult(false, ShopRejection.ContentMissing, null);

                shop._slots.Add(new ShopSlot(i + 1, resolved, ShopSlotState.Available, false));
                shop.MarkSeen(resolved.ContentID);
            }

            return new ShopOpenResult(true, ShopRejection.None, shop);
        }

        /// <summary>
        /// Pins a slot so rerolls leave it alone. Pinning is free.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <returns>The action result.</returns>
        public ShopActionResult Pin(int slot)
        {
            return SetPinned(slot, true);
        }

        /// <summary>
        /// Releases a pin.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <returns>The action result.</returns>
        public ShopActionResult Unpin(int slot)
        {
            return SetPinned(slot, false);
        }

        /// <summary>
        /// What the next reroll would cost and replace.
        /// </summary>
        /// <returns>The preview.</returns>
        public RerollPreview PreviewReroll()
        {
            return ValidateReroll(out List<int> _, out List<string> _);
        }

        /// <summary>
        /// Rerolls every unpinned slot, drawing replacements the shop has not shown as one Determinism
        /// decision. Sold slots are open and are refilled.
        /// </summary>
        /// <returns>The reroll result.</returns>
        public RerollResult Reroll()
        {
            RerollPreview preview = ValidateReroll(out List<int> open, out List<string> unseen);
            if (preview.Rejection != ShopRejection.None)
                return new RerollResult(false, preview.Rejection, null, null, new int[0]);

            int ordinal = _session.Ordinals.Next(Purpose, Definition.ID.Value);
            List<CandidateEntry> candidates = new(unseen.Count);
            for (int i = 0; i < unseen.Count; i++)
            {
                candidates.Add(new CandidateEntry(
                    unseen[i],
                    new CandidateOrderingKey(unseen[i], unseen[i], null, null),
                    null));
            }

            DecisionResult decision = _service.Decide(
                BuildRequest(ordinal, open.Count),
                CandidateSnapshot.Create(candidates));

            if (decision.Outcome.Disposition != DecisionDisposition.Selected)
                return new RerollResult(false, ShopRejection.InsufficientUnseenStock, null, decision.Record, new int[0]);

            TokenLedgerResult debit = _session.Economy.Tokens.Debit(
                new TokenAmount(preview.Cost),
                TokenBasis.RerollCost,
                "reroll:" + (RerollsUsed + 1).ToString(CultureInfo.InvariantCulture));

            List<TransactionConsequence> consequences = new()
            {
                new TokenConsequence(new TokenAmount(preview.Cost), true, TokenBasis.RerollCost, debit.Balance)
            };

            for (int i = 0; i < open.Count; i++)
            {
                int index = open[i];
                ShopSlot slot = _slots[index];
                string contentID = decision.Outcome.SelectedIdentities[i];
                int price = PriceOf(contentID);
                if (!TryResolve(Definition.ID.Value + ":reroll:" + ordinal + ":" + slot.Number, contentID, price, out ShopSlotOffer resolved))
                    throw new ArgumentException("The reroll pool names '" + contentID + "', which the catalog does not supply.", nameof(contentID));

                consequences.Add(new OfferReplaced(slot.Number, slot.Offer.ContentID, contentID));
                _slots[index] = slot with { Offer = resolved, State = ShopSlotState.Available };
                MarkSeen(contentID);
            }

            RerollsUsed += 1;
            TransactionRecord record = new(
                TransactionKind.Reroll,
                _session.Economy.NextTransactionIdentity(TransactionKind.Reroll),
                consequences);
            Record(record);
            return new RerollResult(true, ShopRejection.None, record, decision.Record, SlotNumbers(open));
        }
        
        /// <summary>
        /// What buying a slot would cost.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <returns>The preview.</returns>
        public PurchasePreview PreviewPurchase(int slot)
        {
            return ValidatePurchase(slot, out ShopSlot _, out int _);
        }

        /// <summary>
        /// Buys a routine offer: an Instruction, Structure or Directive goes to the Repository, a
        /// Utility becomes a commitment for this shop's target Process. Patch and Dependency offers are
        /// not routine — they need a host or a RAM decision, so they have their own entry points.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <returns>The purchase result.</returns>
        public PurchaseResult Purchase(int slot)
        {
            PurchasePreview preview = ValidatePurchase(slot, out ShopSlot found, out int index);
            if (preview.Rejection != ShopRejection.None)
                return new PurchaseResult(false, preview.Rejection, null, null, null);

            if (found.Offer.Category == ContentCategory.Patch || found.Offer.Category == ContentCategory.Dependency)
                return new PurchaseResult(false, ShopRejection.NotARoutinePurchase, null, null, null);

            TokenLedgerResult debit = _session.Economy.Tokens.Debit(
                new TokenAmount(found.Offer.Price),
                TokenBasis.OfferPrice,
                found.Offer.OfferIdentity);

            List<TransactionConsequence> consequences = new()
            {
                new TokenConsequence(new TokenAmount(found.Offer.Price), true, TokenBasis.OfferPrice, debit.Balance)
            };

            RepositoryEntry acquired = null;
            UtilityCommitment committed = null;
            if (found.Offer.Category == ContentCategory.Utility)
            {
                UtilityCommitmentResult commitment = _session.Economy.CommitUtility(
                    (UtilityDefinition)found.Offer.Definition,
                    TargetProcess,
                    found.Offer.OfferIdentity);

                committed = commitment.Commitment;
                consequences.Add(new UtilityCommitted(committed.Identity, found.Offer.ContentID, TargetProcess));
            }
            else
            {
                AcquisitionResult acquisition = _session.Repository.Acquire(found.Offer.Definition, AcquisitionOrigin.Purchase);
                acquired = acquisition.Entry;
                consequences.Add(new ItemAcquired(acquired.Item.InstanceID, found.Offer.ContentID, AcquisitionOrigin.Purchase));
            }

            _slots[index] = found with { State = ShopSlotState.Sold, Pinned = false };
            TransactionRecord record = new(
                TransactionKind.Purchase,
                _session.Economy.NextTransactionIdentity(TransactionKind.Purchase),
                consequences);
            Record(record);
            return new PurchaseResult(true, ShopRejection.None, record, acquired, committed);
        }

        /// <summary>
        /// What buying a Patch offer onto a host would cost.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <param name="host">The host instance.</param>
        /// <param name="socket">The socket to occupy.</param>
        /// <returns>The attachment preview.</returns>
        public PatchAttachmentPreview PreviewPatchPurchase(int slot, InstanceID host, int socket)
        {
            if (!TryPatchRequest(slot, host, socket, out PatchAttachmentRequest request, out PatchAttachmentRejection rejection, out ShopSlot _, out int _))
                return new PatchAttachmentPreview(rejection, false, null, 0, 0, _session.Economy.Tokens.Balance, _session.Economy.Tokens.Balance);

            return new PatchInstaller(_session).Preview(request);
        }

        /// <summary>
        /// Buys a Patch offer and sockets it. Replacing an occupied socket is a Service
        /// (CAB-TAX-010 lists PATCH REPLACEMENT among them), so a Services-disabled shop sells a Patch
        /// only into an empty socket.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <param name="host">The host instance.</param>
        /// <param name="socket">The socket to occupy.</param>
        /// <returns>The attachment result.</returns>
        public PatchAttachmentResult PurchasePatch(int slot, InstanceID host, int socket)
        {
            if (!TryPatchRequest(slot, host, socket, out PatchAttachmentRequest request, out PatchAttachmentRejection rejection, out ShopSlot found, out int index))
                return new PatchAttachmentResult(false, rejection, null, null);

            PatchAttachmentResult result = new PatchInstaller(_session).Attach(request);
            if (!result.Succeeded)
                return result;

            _slots[index] = found with { State = ShopSlotState.Sold, Pinned = false };
            Record(result.Record);
            return result;
        }

        /// <summary>
        /// What buying a Dependency offer would do to RAM and Tokens.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <param name="destroyFirst">The installed Dependencies to destroy first.</param>
        /// <returns>The installation preview.</returns>
        public DependencyInstallationPreview PreviewDependencyPurchase(int slot, IReadOnlyList<InstanceID> destroyFirst)
        {
            if (!TryDependencyRequest(slot, destroyFirst, out DependencyInstallationRequest request, out DependencyRejection rejection, out ShopSlot _, out int _))
            {
                TokenAmount balance = _session.Economy.Tokens.Balance;
                DependencyRack rack = _session.Economy.Dependencies;
                return new DependencyInstallationPreview(rejection, rack.Usage, 0, rack.Usage, rack.Capacity, 0, 0, balance, balance);
            }

            return new DependencyInstaller(_session).Preview(request);
        }

        /// <summary>
        /// Buys a Dependency offer and installs it, destroying any the caller names first.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <param name="destroyFirst">The installed Dependencies to destroy first.</param>
        /// <returns>The installation result.</returns>
        public DependencyInstallationResult PurchaseDependency(int slot, IReadOnlyList<InstanceID> destroyFirst)
        {
            if (!TryDependencyRequest(slot, destroyFirst, out DependencyInstallationRequest request, out DependencyRejection rejection, out ShopSlot found, out int index))
                return new DependencyInstallationResult(false, rejection, null, null);

            DependencyInstallationResult result = new DependencyInstaller(_session).Install(request);
            if (!result.Succeeded)
                return result;

            _slots[index] = found with { State = ShopSlotState.Sold, Pinned = false };
            Record(result.Record);
            return result;
        }

        /// <summary>
        /// Destroys an installed Dependency for its refund. Offered only where Dependencies are.
        /// </summary>
        /// <param name="dependency">The instance to destroy.</param>
        /// <returns>The destruction result.</returns>
        public DependencyDestructionResult DestroyDependency(InstanceID dependency)
        {
            if (!IsOpen || !Definition.DependenciesEnabled)
                return new DependencyDestructionResult(false, DependencyRejection.DependenciesNotOffered, null, 0);

            DependencyDestructionResult result = new DependencyInstaller(_session).Destroy(dependency);
            if (result.Succeeded)
                Record(result.Record);

            return result;
        }

        /// <summary>
        /// Deletes a Repository item. A Service, so it needs a Services-offering shop.
        /// </summary>
        /// <param name="item">The instance to delete.</param>
        /// <returns>The Service result.</returns>
        public ServiceResult DeleteItem(InstanceID item)
        {
            if (!IsOpen || !Definition.ServicesEnabled)
                return new ServiceResult(false, ServiceRejection.ServicesNotOffered, null, null);

            ServiceResult result = new RepositoryServices(_session).Delete(item);
            if (result.Succeeded)
                Record(result.Record);

            return result;
        }

        /// <summary>
        /// Duplicates a Repository item. A Service, so it needs a Services-offering shop.
        /// </summary>
        /// <param name="item">The instance to duplicate.</param>
        /// <returns>The Service result.</returns>
        public ServiceResult DuplicateItem(InstanceID item)
        {
            if (!IsOpen || !Definition.ServicesEnabled)
                return new ServiceResult(false, ServiceRejection.ServicesNotOffered, null, null);

            ServiceResult result = new RepositoryServices(_session).Duplicate(item);
            if (result.Succeeded)
                Record(result.Record);

            return result;
        }

        /// <summary>
        /// Closes the shop: pins are released and every later operation rejects.
        /// </summary>
        public void Close()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i] = _slots[i] with { Pinned = false };
            }

            IsOpen = false;
        }

        /// <summary>
        /// Validates a reroll and collects what it would replace and draw from.
        /// </summary>
        /// <param name="open">The slot indexes a reroll would replace.</param>
        /// <param name="unseen">The pool content this shop has not shown.</param>
        /// <returns>The preview describing the outcome.</returns>
        private RerollPreview ValidateReroll(out List<int> open, out List<string> unseen)
        {
            open = new List<int>();
            unseen = new List<string>();
            TokenAmount balance = _session.Economy.Tokens.Balance;
            if (!IsOpen)
                return new RerollPreview(ShopRejection.ShopClosed, 0, 0, 0, balance);

            if (!Definition.RerollsEnabled)
                return new RerollPreview(ShopRejection.RerollsDisabled, 0, 0, 0, balance);

            if (RerollsUsed >= MaximumRerolls)
                return new RerollPreview(ShopRejection.RerollLimitReached, 0, 0, 0, balance);

            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].Pinned)
                    open.Add(i);
            }

            if (open.Count == 0)
                return new RerollPreview(ShopRejection.NoUnpinnedSlot, 0, 0, 0, balance);

            int cost = NextRerollCost.Value;
            if (!_session.Economy.Tokens.CanAfford(new TokenAmount(cost)))
                return new RerollPreview(ShopRejection.InsufficientTokens, cost, open.Count, 0, balance);

            IReadOnlyList<PoolMember> members = _pool.Members;
            for (int i = 0; i < members.Count; i++)
            {
                if (!HasSeen(members[i].Content))
                    unseen.Add(members[i].Content);
            }

            if (unseen.Count < open.Count)
            {
                return new RerollPreview(ShopRejection.InsufficientUnseenStock, cost, open.Count, unseen.Count, balance);
            }

            return new RerollPreview(
                ShopRejection.None,
                cost,
                open.Count,
                unseen.Count,
                new TokenAmount(balance.Value - cost));
        }

        /// <summary>
        /// Builds the decision declaration for one reroll, mirroring the exposure draw's shape.
        /// </summary>
        /// <param name="ordinal">The Session-scoped occurrence ordinal for this purpose and shop.</param>
        /// <param name="count">How many replacements to select.</param>
        /// <returns>The complete request.</returns>
        private DecisionRequest BuildRequest(int ordinal, int count)
        {
            DecisionContextComponents components = new(
                _session.SessionSeedIdentity,
                _session.CatalogRevision,
                DeterminismService.RevisionIdentity,
                _session.SystemIdentity,
                TargetProcess.Value,
                Definition.ID.Value,
                null,
                null,
                Purpose,
                ordinal
            );

            return new DecisionRequest(
                Definition.ID.Value + ":reroll:" + ordinal.ToString(CultureInfo.InvariantCulture),
                Purpose,
                SelectionBoundaryIdentity,
                CandidateSourceIdentity,
                EligibilityRuleIdentity,
                SnapshotTimingIdentity,
                OrderingRuleIdentity,
                count,
                SelectionMethod.UniformSelectionWithoutReplacement,
                ReplacementBehavior.RemovedFromLongerLivedPopulation,
                null,
                TieBreakRuleIdentity,
                null,
                InsufficientCandidateBehavior.CancelTheDecision,
                DecisionContext.Derive(components)
            );
        }
        
        /// <summary>
        /// Pins or unpins a slot, recording the action. Pinning is free, so there is no price path.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <param name="pinned">Whether to pin rather than unpin.</param>
        /// <returns>The action result.</returns>
        private ShopActionResult SetPinned(int slot, bool pinned)
        {
            if (!IsOpen)
                return new ShopActionResult(false, ShopRejection.ShopClosed);

            if (!Definition.PinningEnabled)
                return new ShopActionResult(false, ShopRejection.PinningDisabled);

            if (!TryFindSlot(slot, out ShopSlot found, out int index))
                return new ShopActionResult(false, ShopRejection.UnknownSlot);

            _slots[index] = found with { Pinned = pinned };
            _pins.Add(new PinRecord(slot, pinned));
            return new ShopActionResult(true, ShopRejection.None);
        }

        /// <summary>
        /// Validates a routine purchase: the shop is open, the slot exists and is available, and the
        /// price is covered.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <param name="found">The resolved slot, or null.</param>
        /// <param name="index">The slot's index in the slot list, or -1.</param>
        /// <returns>The preview describing the outcome.</returns>
        private PurchasePreview ValidatePurchase(int slot, out ShopSlot found, out int index)
        {
            found = null;
            index = -1;
            TokenAmount balance = _session.Economy.Tokens.Balance;
            if (!IsOpen)
                return new PurchasePreview(ShopRejection.ShopClosed, 0, balance, default);

            if (!TryFindSlot(slot, out ShopSlot candidate, out int candidateIndex))
                return new PurchasePreview(ShopRejection.UnknownSlot, 0, balance, default);

            if (candidate.State != ShopSlotState.Available)
                return new PurchasePreview(ShopRejection.SlotNotAvailable, 0, balance, candidate.Offer.Category);

            if (!_session.Economy.Tokens.CanAfford(new TokenAmount(candidate.Offer.Price)))
            {
                return new PurchasePreview(
                    ShopRejection.InsufficientTokens,
                    candidate.Offer.Price,
                    balance,
                    candidate.Offer.Category);
            }

            found = candidate;
            index = candidateIndex;
            return new PurchasePreview(
                ShopRejection.None,
                candidate.Offer.Price,
                new TokenAmount(balance.Value - candidate.Offer.Price),
                candidate.Offer.Category);
        }

        /// <summary>
        /// Builds the attachment request behind a Patch offer, applying the shop's own gates first.
        /// Replacing an occupied socket is a Service, so a Services-disabled shop refuses it here
        /// rather than letting the installer charge a fee the shop does not sell.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <param name="host">The host instance.</param>
        /// <param name="socket">The socket to occupy.</param>
        /// <param name="request">The built request, or null.</param>
        /// <param name="rejection">Why the shop refused, when it did.</param>
        /// <param name="found">The resolved slot, or null.</param>
        /// <param name="index">The slot's index, or -1.</param>
        /// <returns>True when a request was built.</returns>
        private bool TryPatchRequest(
            int slot,
            InstanceID host,
            int socket,
            out PatchAttachmentRequest request,
            out PatchAttachmentRejection rejection,
            out ShopSlot found,
            out int index
        )
        {
            request = null;
            rejection = PatchAttachmentRejection.None;
            found = null;
            index = -1;
            if (!IsOpen)
            {
                rejection = PatchAttachmentRejection.ServicesNotOffered;
                return false;
            }

            if (!TryFindSlot(slot, out ShopSlot candidate, out int candidateIndex))
            {
                rejection = PatchAttachmentRejection.UnknownHost;
                return false;
            }

            if (candidate.State != ShopSlotState.Available || candidate.Offer.Category != ContentCategory.Patch)
            {
                rejection = PatchAttachmentRejection.UnknownHost;
                return false;
            }

            if (!_session.Repository.TryGet(host, out RepositoryEntry entry))
            {
                rejection = PatchAttachmentRejection.UnknownHost;
                return false;
            }

            if (entry.Item.Kind == RepositoryItemKind.Instruction
                && entry.Item.Instruction.TryGetAttachment(socket, out PatchAttachment _)
                && !Definition.ServicesEnabled)
            {
                rejection = PatchAttachmentRejection.ServicesNotOffered;
                return false;
            }

            found = candidate;
            index = candidateIndex;
            request = new PatchAttachmentRequest(
                (PatchDefinition)candidate.Offer.Definition,
                host,
                socket,
                candidate.Offer.Price,
                null,
                candidate.Offer.OfferIdentity);

            return true;
        }

        /// <summary>
        /// Builds the installation request behind a Dependency offer, applying the shop's own gate
        /// first.
        /// </summary>
        /// <param name="slot">The slot number.</param>
        /// <param name="destroyFirst">The installed Dependencies to destroy first.</param>
        /// <param name="request">The built request, or null.</param>
        /// <param name="rejection">Why the shop refused, when it did.</param>
        /// <param name="found">The resolved slot, or null.</param>
        /// <param name="index">The slot's index, or -1.</param>
        /// <returns>True when a request was built.</returns>
        private bool TryDependencyRequest(
            int slot,
            IReadOnlyList<InstanceID> destroyFirst,
            out DependencyInstallationRequest request,
            out DependencyRejection rejection,
            out ShopSlot found,
            out int index
        )
        {
            request = null;
            rejection = DependencyRejection.None;
            found = null;
            index = -1;
            if (!IsOpen || !Definition.DependenciesEnabled)
            {
                rejection = DependencyRejection.DependenciesNotOffered;
                return false;
            }

            if (!TryFindSlot(slot, out ShopSlot candidate, out int candidateIndex))
            {
                rejection = DependencyRejection.UnknownInstance;
                return false;
            }

            if (candidate.State != ShopSlotState.Available || candidate.Offer.Category != ContentCategory.Dependency)
            {
                rejection = DependencyRejection.NotADependency;
                return false;
            }

            found = candidate;
            index = candidateIndex;
            request = new DependencyInstallationRequest(
                (DependencyDefinition)candidate.Offer.Definition,
                candidate.Offer.Price,
                DependencyOrigin.Purchase,
                destroyFirst ?? new InstanceID[0],
                candidate.Offer.OfferIdentity);

            return true;
        }

        /// <summary>
        /// Finds a slot by its number.
        /// </summary>
        /// <param name="number">The slot number.</param>
        /// <param name="slot">The slot found, or null.</param>
        /// <param name="index">Its index in the slot list, or -1.</param>
        /// <returns>True when a slot carries that number.</returns>
        private bool TryFindSlot(int number, out ShopSlot slot, out int index)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Number != number)
                    continue;

                slot = _slots[i];
                index = i;
                return true;
            }

            slot = null;
            index = -1;
            return false;
        }

        /// <summary>
        /// Resolves a content ID to a frozen definition and its category, by WB namespace prefix.
        /// </summary>
        /// <param name="offerIdentity">The offer identity to give the resolved offer.</param>
        /// <param name="contentID">The content ID to resolve.</param>
        /// <param name="price">The Tokens it costs.</param>
        /// <param name="offer">The resolved offer, or null.</param>
        /// <returns>True when the catalog supplies it.</returns>
        private bool TryResolve(
            string offerIdentity,
            string contentID,
            int price,
            out ShopSlotOffer offer
        )
        {
            offer = null;
            if (string.IsNullOrEmpty(contentID))
                return false;

            if (contentID.StartsWith("WB-PAT-", StringComparison.Ordinal))
            {
                if (!_catalog.TryGetPatch(new PatchID(contentID), out PatchDefinition patch))
                    return false;

                offer = new ShopSlotOffer(offerIdentity, contentID, ContentCategory.Patch, patch, price);
                return true;
            }

            if (contentID.StartsWith("WB-DEP-", StringComparison.Ordinal))
            {
                if (!_catalog.TryGetDependency(new DependencyID(contentID), out DependencyDefinition dependency))
                    return false;

                offer = new ShopSlotOffer(offerIdentity, contentID, ContentCategory.Dependency, dependency, price);
                return true;
            }

            if (contentID.StartsWith("WB-UTL-", StringComparison.Ordinal))
            {
                if (!_catalog.TryGetUtility(new UtilityID(contentID), out UtilityDefinition utility))
                    return false;

                offer = new ShopSlotOffer(offerIdentity, contentID, ContentCategory.Utility, utility, price);
                return true;
            }

            if (!_catalog.TryGetItem(contentID, out ContentDefinition item))
                return false;

            offer = new ShopSlotOffer(offerIdentity, contentID, item.Category, item, price);
            return true;
        }

        /// <summary>
        /// The price a pool member carries, falling back to the shop's first fixed-offer price when the
        /// membership declares none — a pool member's price belongs to the membership (Balance section
        /// 7.7), so an unpriced member takes the shop's own band rather than inventing one.
        /// </summary>
        /// <param name="contentID">The member's content ID.</param>
        /// <returns>The price in Tokens.</returns>
        private int PriceOf(string contentID)
        {
            IReadOnlyList<PoolMember> members = _pool.Members;
            for (int i = 0; i < members.Count; i++)
            {
                if (!string.Equals(members[i].Content, contentID, StringComparison.Ordinal))
                    continue;

                if (members[i].Price.HasValue)
                    return members[i].Price.Value;

                break;
            }

            return Definition.FixedOffers.Count > 0 ? Definition.FixedOffers[0].Price : 0;
        }

        /// <summary>
        /// Records a content ID as shown in this shop, so no reroll offers it again.
        /// </summary>
        /// <param name="contentID">The content ID.</param>
        private void MarkSeen(string contentID)
        {
            if (!HasSeen(contentID))
                _seen.Add(contentID);
        }

        /// <summary>
        /// Whether this shop has already shown a content ID.
        /// </summary>
        /// <param name="contentID">The content ID.</param>
        /// <returns>True when it has been shown.</returns>
        private bool HasSeen(string contentID)
        {
            for (int i = 0; i < _seen.Count; i++)
            {
                if (string.Equals(_seen[i], contentID, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Files a committed transaction under both this shop and the Session, so a shop's own history
        /// reads without filtering the Session's.
        /// </summary>
        /// <param name="record">The transaction record.</param>
        private void Record(TransactionRecord record)
        {
            _transactions.Add(record);
            if (!HoldsRecord(record))
                _session.Economy.Append(record);
        }

        /// <summary>
        /// Whether the Session already holds a record, which it does whenever an installer or Service
        /// appended it as part of its own commit.
        /// </summary>
        /// <param name="record">The transaction record.</param>
        /// <returns>True when the Session already holds it.</returns>
        private bool HoldsRecord(TransactionRecord record)
        {
            IReadOnlyList<TransactionRecord> records = _session.Economy.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (ReferenceEquals(records[i], record))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The slot numbers behind a list of slot indexes.
        /// </summary>
        /// <param name="indexes">The slot indexes.</param>
        /// <returns>The slot numbers, in the same order.</returns>
        private IReadOnlyList<int> SlotNumbers(IReadOnlyList<int> indexes)
        {
            List<int> numbers = new List<int>(indexes.Count);
            for (int i = 0; i < indexes.Count; i++)
            {
                numbers.Add(_slots[indexes[i]].Number);
            }

            return numbers;
        }
    }
}