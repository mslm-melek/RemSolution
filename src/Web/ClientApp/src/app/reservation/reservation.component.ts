import { Component, OnInit } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Router } from '@angular/router';
import { ReservationsClient, ReservationDto, ReservationStatus } from '../web-api-client';

@Component({
  selector: 'app-reservation',
  templateUrl: './reservation.component.html',
  styleUrls: ['./reservation.component.css']
})
export class ReservationComponent implements OnInit {
  reservations: ReservationDto[] = [];
  displayedColumns: string[] = ['car', 'client', 'period', 'status', 'expires', 'actions'];

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;
  status: ReservationStatus | null = null;

  ReservationStatus = ReservationStatus;
  statuses = [
    { value: ReservationStatus.Pending, label: 'Pending' },
    { value: ReservationStatus.Confirmed, label: 'Confirmed' },
    { value: ReservationStatus.Cancelled, label: 'Cancelled' },
    { value: ReservationStatus.Expired, label: 'Expired' }
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

  statusLabel(status?: ReservationStatus): string {
    return this.statuses.find(s => s.value === status)?.label ?? '';
  }

  isPending(r: ReservationDto): boolean {
    return r.status === ReservationStatus.Pending;
  }

  confirm(r: ReservationDto) {
    if (!r.id) return;
    if (!confirm('Confirm this reservation into a renting?')) return;
    this.client.confirmReservation(r.id).subscribe({
      next: rentingId => this.router.navigate(['/renting', rentingId]),
      error: err => console.error(err)
    });
  }

  cancel(r: ReservationDto) {
    if (!r.id) return;
    if (!confirm('Cancel this reservation?')) return;
    this.client.cancelReservation(r.id).subscribe({
      next: () => this.load(),
      error: err => console.error(err)
    });
  }
}
