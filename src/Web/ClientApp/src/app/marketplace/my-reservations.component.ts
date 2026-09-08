import { Component, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  MarketplaceClient, MyReservationDto, ReservationRequirementDto,
  ReservationRequirementKind, ReservationRequirementStatus, ReservationStatus
} from '../web-api-client';
import { TranslocoService } from '@jsverse/transloco';
import { reservationStatusLabelKey, reservationStatusTone } from '../shared/reservation-status';
import {
  isRequirementSettled, requirementKindLabelKey, requirementStatusLabelKey
} from '../shared/reservation-requirements';
import { reportStatusLabelKey, reportStatusTone } from '../shared/agency-reports';
import { ReportDialogComponent, ReportDialogData } from './report-dialog.component';

@Component({
  selector: 'app-my-reservations',
  templateUrl: './my-reservations.component.html',
  styleUrls: ['./my-reservations.component.css']
})
export class MyReservationsComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  reservations: MyReservationDto[] = [];
  loading = true;
  error = '';

  /** What each confirmed reservation still asks of the customer, by reservation id. */
  requirements: Record<number, ReservationRequirementDto[]> = {};
  /** The requirement currently being answered, so only its row shows a spinner. */
  submitting?: number;

  ReservationStatus = ReservationStatus;
  ReservationRequirementKind = ReservationRequirementKind;
  ReservationRequirementStatus = ReservationRequirementStatus;
  readonly requirementKindLabelKey = requirementKindLabelKey;
  readonly requirementStatusLabelKey = requirementStatusLabelKey;
  readonly isRequirementSettled = isRequirementSettled;
  readonly reportStatusLabelKey = reportStatusLabelKey;
  readonly reportStatusTone = reportStatusTone;

  constructor(private client: MarketplaceClient, private dialog: MatDialog) { }

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading = true;
    this.client.getMyReservations().subscribe({
      next: list => {
        this.reservations = list || [];
        this.loadRequirements();
      },
      error: () => { this.error = this.transloco.translate('marketplace.loadFailed'); this.loading = false; }
    });
  }

  /**
   * Only the confirmed ones can ask for anything (see the command), so only
   * those are read — one call each rather than a list-wide one, because a
   * customer has a handful of bookings, not a page of them.
   */
  private loadRequirements() {
    const confirmed = this.reservations.filter(r =>
      r.id && (r.status === ReservationStatus.Confirmed || r.status === ReservationStatus.Paid));

    if (!confirmed.length) {
      this.requirements = {};
      this.loading = false;
      return;
    }

    forkJoin(confirmed.map(r =>
      this.client.getMyReservationRequirements(r.id!).pipe(catchError(() => of([])))
    )).subscribe(lists => {
      const map: Record<number, ReservationRequirementDto[]> = {};
      confirmed.forEach((reservation, index) => map[reservation.id!] = lists[index] || []);
      this.requirements = map;
      this.loading = false;
    });
  }

  asksFor(r: MyReservationDto): ReservationRequirementDto[] {
    return (r.id && this.requirements[r.id]) || [];
  }

  /** Answering terms is a click; everything else needs a file or a word. */
  accept(requirement: ReservationRequirementDto) {
    this.submit(requirement, undefined);
  }

  chooseFile(requirement: ReservationRequirementDto, event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    // Cleared so picking the same file twice still fires a change event.
    input.value = '';

    if (file) this.submit(requirement, file);
  }

  private submit(requirement: ReservationRequirementDto, file?: File) {
    if (!requirement.id) return;

    this.error = '';
    this.submitting = requirement.id;

    const note = file
      ? undefined
      : this.transloco.translate('marketplace.requirements.accepted');

    this.client.submitMyReservationRequirement(
      requirement.id, note, file ? { data: file, fileName: file.name } : undefined
    ).subscribe({
      next: () => { this.submitting = undefined; this.load(); },
      error: () => {
        this.submitting = undefined;
        this.error = this.transloco.translate('marketplace.requirements.submitFailed');
      }
    });
  }

  // Returns a transloco key; the template pipes it.
  statusLabelKey(status?: ReservationStatus): string {
    return reservationStatusLabelKey(status);
  }

  /** Tone class for the global `.chip` — see shared/reservation-status. */
  statusTone(status?: ReservationStatus): string {
    return reservationStatusTone(status);
  }

  isPending(r: MyReservationDto): boolean {
    return r.status === ReservationStatus.PendingConfirmation;
  }

  /**
   * Cancelling asks two things: whether they mean it — with the cost named when
   * there is one, since finding out afterwards is how a fee becomes a dispute —
   * and why, which is what the agency can actually act on.
   */
  cancel(r: MyReservationDto) {
    if (!r.id) return;

    const fee = r.cancellationFeeIfCancelledNow;

    const question = fee
      ? this.transloco.translate('marketplace.confirmCancelWithFee', {
          amount: fee.amount, currency: fee.currency
        })
      : this.transloco.translate('marketplace.confirmCancel');

    if (!confirm(question)) return;

    const reason = prompt(this.transloco.translate('marketplace.promptCancelReason')) ?? undefined;

    this.client.cancelMyReservation(r.id, reason).subscribe({
      next: () => this.load(),
      error: () => this.error = this.transloco.translate('marketplace.cancelFailed')
    });
  }

  /**
   * Taking a booking to the platform. Whether it may be reported at all is the
   * server's answer (`canReport`), so the button and the command cannot disagree.
   */
  report(r: MyReservationDto) {
    if (!r.id) return;

    this.dialog.open(ReportDialogComponent, {
      data: {
        reservationId: r.id,
        agencyName: r.agencyName,
        bookingLabel: `${r.carBrandName ?? ''} ${r.carModelName ?? ''}`.trim(),
        cancelledByAgency: r.cancelledByAgency
      } as ReportDialogData
    }).afterClosed().subscribe(sent => { if (sent) this.load(); });
  }
}
