import { Component, OnInit } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { RentingsClient, RentingDto, RentingState } from '../web-api-client';

@Component({
  selector: 'app-renting',
  templateUrl: './renting.component.html',
  styleUrls: ['./renting.component.css']
})
export class RentingComponent implements OnInit {
  rentings: RentingDto[] = [];
  displayedColumns: string[] = ['car', 'client', 'period', 'state', 'price', 'actions'];

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;
  state: RentingState | null = null;

  RentingState = RentingState;
  states = [
    { value: RentingState.NotYet, label: 'Upcoming' },
    { value: RentingState.InProgress, label: 'In progress' },
    { value: RentingState.Done, label: 'Completed' },
    { value: RentingState.Cancelled, label: 'Cancelled' }
  ];

  constructor(private client: RentingsClient) { }

  ngOnInit() {
    this.load();
  }

  load() {
    this.client.getRentings(this.pageNumber, this.pageSize, null, null, this.state, null, null).subscribe({
      next: result => {
        this.rentings = result.items || [];
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

  stateLabel(state?: RentingState): string {
    return this.states.find(s => s.value === state)?.label ?? '';
  }

  canCancel(renting: RentingDto): boolean {
    return renting.rentingState === RentingState.NotYet
      || renting.rentingState === RentingState.InProgress;
  }

  cancelRenting(renting: RentingDto) {
    if (!renting.id) return;
    if (confirm('Cancel this renting? It stays on record as cancelled.')) {
      this.client.cancelRenting(renting.id).subscribe({
        next: () => this.load(),
        error: err => console.error(err)
      });
    }
  }
}
