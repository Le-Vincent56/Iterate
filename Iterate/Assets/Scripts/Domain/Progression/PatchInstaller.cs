using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Sockets Patches onto Repository items. Every attachment validates completely before anything
    /// commits: a refused request leaves the ledger, the Repository entry and the outstanding grants
    /// exactly as they were. <see cref="Preview"/> and <see cref="Attach"/> share one validation, so
    /// what a player is shown is what the commit will do.
    /// </summary>
    public sealed class PatchInstaller
    {
        private readonly SessionState _session;

        public PatchInstaller(SessionState session)
        {
            _session = session ?? throw new ArgumentException("An installer requires a Session.", nameof(session));
        }

        /// <summary>
        /// The Repository entries a Patch may socket to.
        /// </summary>
        /// <param name="patch">The Patch definition.</param>
        /// <returns>The eligible entries, in Repository order.</returns>
        public IReadOnlyList<RepositoryEntry> EligibleHosts(PatchDefinition patch)
        {
            List<RepositoryEntry> eligible = new List<RepositoryEntry>();
            IReadOnlyList<RepositoryEntry> entries = _session.Repository.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (PatchHostRules.IsEligible(patch, entries[i]))
                    eligible.Add(entries[i]);
            }

            return eligible;
        }

        /// <summary>
        /// What an attachment would do, without doing it.
        /// </summary>
        /// <param name="request">The attachment request.</param>
        /// <returns>The preview.</returns>
        public PatchAttachmentPreview Preview(PatchAttachmentRequest request)
        {
            return Validate(request, out RepositoryEntry _);
        }

        /// <summary>
        /// Sockets a Patch, debiting its price and, when it replaces an occupied socket, the
        /// replacement fee. The prior Patch is destroyed; its instance identity never recurs.
        /// </summary>
        /// <param name="request">The attachment request.</param>
        /// <returns>The attachment result.</returns>
        public PatchAttachmentResult Attach(PatchAttachmentRequest request)
        {
            PatchAttachmentPreview preview = Validate(request, out RepositoryEntry entry);
            if (preview.Rejection != PatchAttachmentRejection.None)
                return new PatchAttachmentResult(false, preview.Rejection, null, null);

            List<TransactionConsequence> consequences = new List<TransactionConsequence>();
            if (preview.Price > 0)
            {
                TokenLedgerResult debit = _session.Economy.Tokens.Debit(
                    new TokenAmount(preview.Price),
                    TokenBasis.OfferPrice,
                    request.Reference);
                consequences.Add(new TokenConsequence(new TokenAmount(preview.Price), true, TokenBasis.OfferPrice, debit.Balance));
            }

            if (preview.Fee > 0)
            {
                TokenLedgerResult fee = _session.Economy.Tokens.Debit(
                    new TokenAmount(preview.Fee),
                    TokenBasis.ReplacementFee,
                    request.Patch.ID.Value);
                consequences.Add(new TokenConsequence(new TokenAmount(preview.Fee), true, TokenBasis.ReplacementFee, fee.Balance));
            }

            if (preview.IsReplacement)
            {
                consequences.Add(new PatchDestroyed(
                    request.Host,
                    request.Socket,
                    preview.Replaced.InstanceID,
                    preview.Replaced.Definition.ID.Value));
            }

            PatchInstance minted = new(_session.InstanceIDs.Next(), request.Patch);
            InstructionInstance host = entry.Item.Instruction.WithAttachment(new PatchAttachment(request.Socket, minted));
            _session.Repository.Replace(request.Host, entry.Item with { Instruction = host });

            if (request.Grant.HasValue)
                _session.Economy.ConsumeGrant(request.Grant.Value);

            consequences.Add(new PatchAttached(request.Host, request.Socket, minted.InstanceID, request.Patch.ID.Value));

            TransactionRecord record = new(
                TransactionKind.PatchAttachment,
                _session.Economy.NextTransactionIdentity(TransactionKind.PatchAttachment),
                consequences);
            _session.Economy.Append(record);
            return new PatchAttachmentResult(true, PatchAttachmentRejection.None, record, minted);
        }

        /// <summary>
        /// The one validation both entry points run. Order matters: what is wrong with the host is
        /// reported before what is wrong with the price, so a player attaching to an illegal host is
        /// not told they are short of Tokens.
        /// </summary>
        /// <param name="request">The attachment request.</param>
        /// <param name="entry">The resolved host entry, or null.</param>
        /// <returns>The preview describing the request's outcome.</returns>
        private PatchAttachmentPreview Validate(PatchAttachmentRequest request, out RepositoryEntry entry)
        {
            entry = null;
            TokenAmount before = _session.Economy.Tokens.Balance;
            if (request == null)
                throw new ArgumentException("An attachment requires a request.", nameof(request));

            if (!_session.Repository.TryGet(request.Host, out RepositoryEntry found))
                return Refused(PatchAttachmentRejection.UnknownHost, before);

            if (found.Item.Kind != RepositoryItemKind.Instruction)
                return Refused(PatchAttachmentRejection.HostNotAnInstruction, before);

            if (!PatchHostRules.IsEligible(request.Patch, found))
                return Refused(PatchAttachmentRejection.HostIneligible, before);

            int sockets = _session.Economy.Parameters.PatchSocketsPerRepositoryInstance;
            if (request.Socket < 1 || request.Socket > sockets)
                return Refused(PatchAttachmentRejection.SocketOutOfRange, before);

            if (request.Grant.HasValue)
            {
                if (!_session.Economy.TryGetGrant(request.Grant.Value, out PatchGrant grant))
                    return Refused(PatchAttachmentRejection.UnknownGrant, before);

                if (grant.Definition.ID != request.Patch.ID)
                    return Refused(PatchAttachmentRejection.GrantDefinitionMismatch, before);
            }

            bool isReplacement = found.Item.Instruction.TryGetAttachment(request.Socket, out PatchAttachment occupied);
            int fee = isReplacement ? _session.Economy.Parameters.PatchReplacementFee : 0;
            int total = request.Price + fee;
            if (!_session.Economy.Tokens.CanAfford(new TokenAmount(total)))
            {
                return new PatchAttachmentPreview(
                    PatchAttachmentRejection.InsufficientTokens,
                    isReplacement,
                    isReplacement ? occupied.Patch : null,
                    request.Price,
                    fee,
                    before,
                    before);
            }

            entry = found;
            return new PatchAttachmentPreview(
                PatchAttachmentRejection.None,
                isReplacement,
                isReplacement ? occupied.Patch : null,
                request.Price,
                fee,
                before,
                new TokenAmount(before.Value - total));
        }

        /// <summary>
        /// Builds a preview for a request refused before its price could be computed.
        /// </summary>
        /// <param name="rejection">Why it was refused.</param>
        /// <param name="before">The unchanged balance.</param>
        /// <returns>The refusing preview.</returns>
        private static PatchAttachmentPreview Refused(PatchAttachmentRejection rejection, TokenAmount before)
        {
            return new PatchAttachmentPreview(rejection, false, null, 0, 0, before, before);
        }
    }
}