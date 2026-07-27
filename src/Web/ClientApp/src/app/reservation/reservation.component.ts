import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Router } from '@angular/router';
import {
  ReservationsClient, ReservationDto, ReservationStatus, RejectReservationCommand,
  ConvertReservationCommand
} from '../web-api-client';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-reservation',
  templateUrl: './reservation.component.html',
  styleUrls: ['./reservation.component.css']
})
export class ReservationComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  reservations: ReservationDto[] = [];
  displayedColumns: string[] = ['car', 'client', 'period', 'paid', 'status', 'expires', 'actions'];

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;
  status: ReservationStatus | null = null;
  error = '';

  ReservationStatus = ReservationStatus;
  statuses = [
    { value: ReservationStatus.PendingConfirmation, labelKey: 'enums.reservationStatus.pendingConfirmation' },
    { value: ReservationStatus.Confirmed, labelKey: 'enums.reservationStatus.confirmed' },
    { value: ReservationStatus.Paid, labelKey: 'enums.reservationStatus.paid' },
    { value: ReservationStatus.Converted, labelKey: 'enums.reservationStatus.converted' },
    { value: ReservationStatus.Rejected, labelKey: 'enums.reservationStatus.rejected' },
    { value: ReservationStatus.Cancelled, labelKey: 'enums.reservationStatus.cancelled' },
    { value: ReservationStatus.Expired, labelKey: 'enums.reservationStatus.expired' }
  ];

  constructor(private client: ReservationsClient, private router: Router) { }

  ngOnInit() {
    this.load();
  }

  load() {
    this.client.getReservations(this.pageNumber, this.pageSize, null, null, this.status).subscribe({
      next: result => {
        this.reservations = result.items || [];
        this.totalCount = result.totalCount || 0;
      },
      error: err => console.error(err)
    });
  }

  onFilter() {
    this.pageNumber = 1;
    this.load();
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }

  // Returns a transloco key; the template pipes it.
  statusLabelKey(status?: ReservationStatus): string {
    return this.statuses.find(s => s.value === status)?.labelKey ?? '';
  }

  statusClass(status?: ReservationStatus): string {
    switch (status) {
      case ReservationStatus.Confirmed:
      case ReservationStatus.Paid: return 'ok';
      case ReservationStatus.Converted: return 'converted';
      case ReservationStatus.PendingConfirmation: return 'pending';
      case ReservationStatus.Rejected:
      case ReservationStatus.Cancelled:
      case ReservationStatus.Expired: return 'ended';
      default: return '';
    }
  }

  // Why the hold left the happy path, for a tooltip on terminal rows.
  statusReason(r: ReservationDto): string {
    return r.rejectedReason || r.cancelledReason || r.expiredReason || '';
  }

  isPending(r: ReservationDto): boolean {
    return r.status === ReservationStatus.PendingConfirmation;
  }

  isConvertible(r: ReservationDto): boolean {
    return r.status === ReservationStatus.Confirmed || r.status === ReservationStatus.Paid;
  }

  isActive(r: ReservationDto): boolean {
    return this.isPending(r) || this.isConvertible(r);
  }

  confirm(r: ReservationDto) {
    if (!r.id) return;
    this.client.confirmReservation(r.id).subscribe({
      next: () => this.load(),
      error: err => this.fail(err)
    });
  }

  reject(r: ReservationDto) {
    if (!r.id) return;
    const reason = prompt(this.transloco.translate('reservation.promptRejectReason'));
    if (!reason) return;
    this.client.rejectReservation(r.id, new RejectReservationCommand({ id: r.id, reason })).subscribe({
      next: () => this.load(),
      error: err => this.fail(err)
    });
  }

  convert(r: ReservationDto) {
    if (!r.id) return;
    if (!confirm(this.transloco.translate('reservation.confirmConvert'))) return;
    const cin = prompt(this.transloco.translate('reservation.promptDriverCin')) || undefined;
    const passeportNumber = cin ? undefined : (prompt(this.transloco.translate('reservation.promptDriverPassport')) || undefined);
    const command = new ConvertReservationCommand({ id: r.id, cin, passeportNumber });
    this.client.convertReservation(r.id, command).subscribe({
      next: rentingId => this.router.navigate(['/renting', rentingId]),
      error: err => this.fail(err)
    });
  }

  cancel(r: ReservationDto) {
    if (!r.id) return;
    const reason = prompt(this.transloco.translate('reservation.promptCancelReason')) ?? undefined;
    if (reason === undefined && !confirm(this.transloco.translate('reservation.confirmCancel'))) return;
    this.client.cancelReservation(r.id, reason).subscribe({
      next: () => this.load(),
      error: err => this.fail(err)
    });
  }

  private fail(err: any) {
    this.error = err?.response ? this.extract(err.response) : this.transloco.translate('common.actionFailed');
    console.error(err);
    setTimeout(() => this.error = '', 6000);
  }

  private extract(response: string): string {
    try {
      const body = JSON.parse(response);
      if (body?.errors) {
        const messages: string[] = [];
        for (const key of Object.keys(body.errors)) {
          const val = body.errors[key];
          if (Array.isArray(val)) { messages.push(...val); } else { messages.push(String(val)); }
        }
        return messages.join(' ');
      }
      return body?.detail || body?.title || 'The action could not be completed.';
    } catch {
      return 'The action could not be completed.';
    }
  }
}
