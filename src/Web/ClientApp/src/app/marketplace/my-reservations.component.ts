import { Component, OnInit } from '@angular/core';
import { MarketplaceClient, MyReservationDto, ReservationStatus } from '../web-api-client';

@Component({
  selector: 'app-my-reservations',
  templateUrl: './my-reservations.component.html',
  styleUrls: ['./my-reservations.component.css']
})
export class MyReservationsComponent implements OnInit {
  reservations: MyReservationDto[] = [];
  loading = true;
  error = '';

  ReservationStatus = ReservationStatus;
  private labels = ['Pending', 'Confirmed', 'Cancelled', 'Expired'];

  constructor(private client: MarketplaceClient) { }

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading = true;
    this.client.getMyReservations().subscribe({
      next: list => { this.reservations = list || []; this.loading = false; },
      error: () => { this.error = 'Could not load your reservations.'; this.loading = false; }
    });
  }

  statusLabel(status?: ReservationStatus): string {
    return status === undefined || status === null ? '' : this.labels[status] ?? '';
  }

  statusClass(status?: ReservationStatus): string {
    switch (status) {
      case ReservationStatus.Confirmed: return 'confirmed';
      case ReservationStatus.Pending: return 'pending';
      case ReservationStatus.Cancelled: return 'cancelled';
      case ReservationStatus.Expired: return 'expired';
      default: return '';
    }
  }

  isPending(r: MyReservationDto): boolean {
    return r.status === ReservationStatus.Pending;
  }

  cancel(r: MyReservationDto) {
    if (!r.id) return;
    if (!confirm('Cancel this reservation request?')) return;
    this.client.cancelMyReservation(r.id).subscribe({
      next: () => this.load(),
      error: () => this.error = 'Could not cancel the reservation.'
    });
  }
}
