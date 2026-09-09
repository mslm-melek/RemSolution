using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Client
{
    /// <summary>
    /// Erasing one client's personal data, in one place — the command an operator
    /// runs and the retention sweep both come through here, so "erased" means the
    /// same thing however it was triggered.
    /// <para>
    /// WHAT SURVIVES, deliberately: the hires, payments, invoices and contracts.
    /// A rental agency has to keep its books, and a reprinted invoice must still
    /// say what was charged to whom — the generated PDFs therefore keep the
    /// identity details that were printed on them. That is a legal-retention
    /// basis outliving an erasure request, not an oversight; it is also exactly
    /// the "conserving the financial records" the requirement asks for.
    /// </para>
    /// </summary>
    public static class ClientPersonalDataErasure
    {
        /// <summary>
        /// Whether the client is in the middle of something. Erasing the renter of
        /// a car that is still out would leave nobody to hand it back to, so both
        /// callers check this — the command to refuse, the sweep to skip.
        /// </summary>
        public static async Task<bool> HasLiveBookingAsync(
            IApplicationDbContext context, int clientId, CancellationToken cancellationToken)
        {
            var live = await context.Rentings
                .AnyAsync(r => (r.ClientId == clientId || r.SecondClientId == clientId)
                               && (r.RentingState == RentingState.NotYet
                                   || r.RentingState == RentingState.InProgress),
                          cancellationToken);

            if (live) return true;

            // The same three states that hold a car (see the reservation
            // lifecycle): a rejected or expired request holds nothing.
            return await context.Reservations
                .AnyAsync(r => r.ClientId == clientId
                               && (r.Status == ReservationStatus.PendingConfirmation
                                   || r.Status == ReservationStatus.Confirmed
                                   || r.Status == ReservationStatus.Paid),
                          cancellationToken);
        }

        /// <summary>
        /// Empties the client, its document scans, its notification history and
        /// the audit payloads about it. The client must be TRACKED by the given
        /// context, and its tenant must be the ambient one.
        /// </summary>
        public static async Task EraseAsync(
            IApplicationDbContext context,
            IStoredFileService storedFiles,
            IPersonalDataErasureStore store,
            Domain.Entities.Client client,
            DateTimeOffset erasedAt,
            string? reason,
            CancellationToken cancellationToken)
        {
            var agencyId = client.AgencyId;

            // FIRST, before the row itself is touched. Every [Auditable] command
            // that edited this client wrote its whole state into AuditLog as
            // JSON, so those rows are copies of what is about to be destroyed —
            // and doing them first is what makes a retry after a half-failure
            // finish the job: the client is still marked un-erased, so the
            // command runs again from the top.
            await store.RedactAuditTrailAsync(client.Id, cancellationToken);

            var fileIds = client.ErasePersonalData(erasedAt);

            // A notification's text is not stored — but the values interpolated
            // into it are, and they include the client's name; the address it was
            // mailed to is right beside them. The row itself stays: it is the
            // agency's record that a message went out, and that record is about
            // the agency's conduct, not about the person.
            var notifications = await context.Notifications
                .Where(n => n.ClientId == client.Id
                            && (n.ArgsJson != null || n.RecipientEmail != null))
                .ToListAsync(cancellationToken);

            foreach (var notification in notifications)
            {
                notification.ArgsJson = null;
                notification.RecipientEmail = null;
            }

            await context.SaveChangesAsync(cancellationToken);

            // Only once the client is durably detached from them: drop the file
            // records, then the bytes if nothing else points at them (a scan can
            // be shared with another record of the same agency — see
            // StoredFileService's dedup).
            await RemoveFilesAsync(context, storedFiles, fileIds, cancellationToken);

            // Last: nothing else depends on it, and a crash before it costs the
            // record of the erasure rather than leaving anything behind.
            await store.RecordErasureAsync(client.Id, agencyId, reason, cancellationToken);
        }

        private static async Task RemoveFilesAsync(
            IApplicationDbContext context,
            IStoredFileService storedFiles,
            IReadOnlyList<int> fileIds,
            CancellationToken cancellationToken)
        {
            if (fileIds.Count == 0) return;

            var files = await context.StoredFiles
                .Where(f => fileIds.Contains(f.Id))
                .ToListAsync(cancellationToken);

            if (files.Count == 0) return;

            context.StoredFiles.RemoveRange(files);
            await context.SaveChangesAsync(cancellationToken);

            foreach (var file in files)
            {
                await storedFiles.DeletePhysicalIfOrphanAsync(file.Path, file.Url, cancellationToken);
            }
        }
    }
}
