import { Component, OnInit, inject } from '@angular/core';
import { MarketplaceClient, MyReservationDto, ReservationStatus } from '../web-api-client';
import { TranslocoService } from '@jsverse/transloco';

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

  ReservationStatus = ReservationStatus;
  private labelKeys: { [key: number]: string } = {
    [ReservationStatus.PendingConfirmation]: 'enums.reservationStatus.pendingConfirmation',
    [ReservationStatus.Confirmed]: 'enums.reservationStatus.confirmed',
    [ReservationStatus.Cancelled]: 'enums.reservationStatus.cancelled',
    [ReservationStatus.Expired]: 'enums.reservationStatus.expired',
    [ReservationStatus.Rejected]: 'enums.reservationStatus.rejected',
    [ReservationStatus.Paid]: 'enums.reservationStatus.paid',
    [ReservationStatus.Converted]: 'enums.reservationStatus.converted'
  };

  constructor(private client: MarketplaceClient) { }

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading = true;
    this.client.getMyReservations().subscribe({
      next: list => { this.reservations = list || []; this.loading = false; },
      error: () => { this.error = this.transloco.translate('marketplace.loadFailed'); this.loading = false; }
    });
  }

  // Returns a transloco key; the template pipes it.
  statusLabelKey(status?: ReservationStatus): string {
    return status === undefined || status === null ? '' : this.labelKeys[status] ?? '';
  }

  statusClass(status?: ReservationStatus): string {
    switch (status) {
      case ReservationStatus.Confirmed: return 'confirmed';
      case ReservationStatus.Paid: return 'confirmed';
      case ReservationStatus.Converted: return 'confirmed';
      case ReservationStatus.PendingConfirmation: return 'pending';
      case ReservationStatus.Cancelled: return 'cancelled';
      case ReservationStatus.Rejected: return 'cancelled';
      case ReservationStatus.Expired: return 'expired';
      default: return '';
    }
  }

  isPending(r: MyReservationDto): boolean {
    return r.status === ReservationStatus.PendingConfirmation;
  }

  cancel(r: MyReservationDto) {
    if (!r.id) return;
    if (!confirm(this.transloco.translate('marketplace.confirmCancel'))) return;
    this.client.cancelMyReservation(r.id).subscribe({
      next: () => this.load(),
      error: () => this.error = this.transloco.translate('marketplace.cancelFailed')
    });
  }
}
