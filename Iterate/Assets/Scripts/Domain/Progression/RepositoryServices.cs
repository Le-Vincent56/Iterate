using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The Repository Services a shop sells: deleting an item, which destroys its attached Patches
    /// with it and refunds nothing for them, and duplicating one, which produces a fresh unpatched
    /// instance under the next suffix. Starter items are protected from deletion but may be
    /// duplicated, so the two operations validate against different protections.
    /// </summary>
    public sealed class RepositoryServices
    {
        private readonly SessionState _session;

        public RepositoryServices(SessionState session)
        {
            _session = session ?? throw new ArgumentException("A Service requires a Session.", nameof(session));
        }

        /// <summary>
        /// What deleting an item would cost and destroy, without doing it.
        /// </summary>
        /// <param name="item">The instance to delete.</param>
        /// <returns>The preview.</returns>
        public ServicePreview PreviewDelete(InstanceID item)
        {
            return ValidateDelete(item, out RepositoryEntry _);
        }

        /// <summary>
        /// Deletes an item permanently, destroying every Patch attached to it. The Patches carry no
        /// refund, and the freed suffix is never reissued.
        /// </summary>
        /// <param name="item">The instance to delete.</param>
        /// <returns>The Service result.</returns>
        public ServiceResult Delete(InstanceID item)
        {
            ServicePreview preview = ValidateDelete(item, out RepositoryEntry entry);
            if (preview.Rejection != ServiceRejection.None)
                return new ServiceResult(false, preview.Rejection, null, null);

            List<TransactionConsequence> consequences = new List<TransactionConsequence>();
            TokenLedgerResult debit = _session.Economy.Tokens.Debit(
                new TokenAmount(preview.Price),
                TokenBasis.ServicePrice,
                entry.Item.DefinitionID
            );
            consequences.Add(new TokenConsequence(new TokenAmount(preview.Price), true, TokenBasis.ServicePrice, debit.Balance));

            if (entry.Item.Kind == RepositoryItemKind.Instruction)
            {
                IReadOnlyList<PatchAttachment> attachments = entry.Item.Instruction.AttachedPatches;
                for (int i = 0; i < attachments.Count; i++)
                {
                    consequences.Add(new PatchDestroyed(
                        entry.Item.InstanceID,
                        attachments[i].Socket,
                        attachments[i].Patch.InstanceID,
                        attachments[i].Patch.Definition.ID.Value
                    ));
                }
            }

            _session.Repository.Delete(item);
            consequences.Add(new ItemDeleted(entry.Item.InstanceID, entry.Item.DefinitionID));

            TransactionRecord record = new(
                TransactionKind.ItemDeletion,
                _session.Economy.NextTransactionIdentity(TransactionKind.ItemDeletion),
                consequences
            );
            _session.Economy.Append(record);
            return new ServiceResult(true, ServiceRejection.None, record, null);
        }

        /// <summary>
        /// What duplicating an item would cost, without doing it.
        /// </summary>
        /// <param name="item">The instance to duplicate.</param>
        /// <returns>The preview.</returns>
        public ServicePreview PreviewDuplicate(InstanceID item)
        {
            return ValidateDuplicate(item, out RepositoryEntry _);
        }

        /// <summary>
        /// Duplicates an item: a new instance of the same definition, unpatched, under the next suffix
        /// and with acquisition origin Duplicate. The original is untouched, including its Patches.
        /// </summary>
        /// <param name="item">The instance to duplicate.</param>
        /// <returns>The Service result.</returns>
        public ServiceResult Duplicate(InstanceID item)
        {
            ServicePreview preview = ValidateDuplicate(item, out RepositoryEntry entry);
            if (preview.Rejection != ServiceRejection.None)
                return new ServiceResult(false, preview.Rejection, null, null);

            List<TransactionConsequence> consequences = new List<TransactionConsequence>();
            TokenLedgerResult debit = _session.Economy.Tokens.Debit(
                new TokenAmount(preview.Price),
                TokenBasis.ServicePrice,
                entry.Item.DefinitionID
            );
            consequences.Add(new TokenConsequence(new TokenAmount(preview.Price), true, TokenBasis.ServicePrice, debit.Balance));

            AcquisitionResult acquired = _session.Repository.Acquire(
                DefinitionOf(entry.Item),
                AcquisitionOrigin.Duplicate
            );

            consequences.Add(new ItemAcquired(
                acquired.Entry.Item.InstanceID,
                acquired.Entry.Item.DefinitionID,
                AcquisitionOrigin.Duplicate
            ));

            TransactionRecord record = new(
                TransactionKind.ItemDuplication,
                _session.Economy.NextTransactionIdentity(TransactionKind.ItemDuplication),
                consequences
            );
            _session.Economy.Append(record);
            return new ServiceResult(true, ServiceRejection.None, record, acquired.Entry);
        }

        /// <summary>
        /// Validates a deletion: the item exists, is not starter-protected, and the price is covered.
        /// </summary>
        /// <param name="item">The instance to delete.</param>
        /// <param name="entry">The resolved entry, or null.</param>
        /// <returns>The preview describing the outcome.</returns>
        private ServicePreview ValidateDelete(InstanceID item, out RepositoryEntry entry)
        {
            entry = null;
            TokenAmount before = _session.Economy.Tokens.Balance;
            if (!_session.Repository.TryGet(item, out RepositoryEntry found))
                return new ServicePreview(ServiceRejection.UnknownInstance, 0, 0, before);

            if (found.IsStarterProtected)
                return new ServicePreview(ServiceRejection.StarterProtected, 0, 0, before);

            int price = _session.Economy.Parameters.DeleteRepositoryItemPrice;
            int patches = found.Item.Kind == RepositoryItemKind.Instruction
                ? found.Item.Instruction.AttachedPatches.Count
                : 0;

            if (!_session.Economy.Tokens.CanAfford(new TokenAmount(price)))
                return new ServicePreview(ServiceRejection.InsufficientTokens, price, patches, before);

            entry = found;
            return new ServicePreview(
                ServiceRejection.None,
                price,
                patches,
                new TokenAmount(before.Value - price)
            );
        }

        /// <summary>
        /// Validates a duplication: the item exists and the price is covered. Starter protection does
        /// not apply — a starter item may be duplicated (spec section 6).
        /// </summary>
        /// <param name="item">The instance to duplicate.</param>
        /// <param name="entry">The resolved entry, or null.</param>
        /// <returns>The preview describing the outcome.</returns>
        private ServicePreview ValidateDuplicate(InstanceID item, out RepositoryEntry entry)
        {
            entry = null;
            TokenAmount before = _session.Economy.Tokens.Balance;
            if (!_session.Repository.TryGet(item, out RepositoryEntry found))
                return new ServicePreview(ServiceRejection.UnknownInstance, 0, 0, before);

            int price = _session.Economy.Parameters.DuplicateRepositoryItemPrice;
            if (!_session.Economy.Tokens.CanAfford(new TokenAmount(price)))
                return new ServicePreview(ServiceRejection.InsufficientTokens, price, 0, before);

            entry = found;
            return new ServicePreview(
                ServiceRejection.None,
                price,
                0,
                new TokenAmount(before.Value - price));
        }

        /// <summary>
        /// The frozen definition behind a Repository item, whichever of the three kinds it is.
        /// </summary>
        /// <param name="item">The Repository item.</param>
        /// <returns>The content definition.</returns>
        private static ContentDefinition DefinitionOf(RepositoryItem item)
        {
            switch (item.Kind)
            {
                case RepositoryItemKind.Instruction: return item.Instruction.Definition;
                case RepositoryItemKind.Structure: return item.Structure.Definition;
                case RepositoryItemKind.Directive: return item.Directive.Definition;
                default:
                    throw new ArgumentException("Unknown Repository item kind " + item.Kind + ".", nameof(item));
            }
        }
    }
}