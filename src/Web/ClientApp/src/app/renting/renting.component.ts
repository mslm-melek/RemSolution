import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { RentingsClient, RentingDto, RentingState } from '../web-api-client';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-renting',
  templateUrl: './renting.component.html',
  styleUrls: ['./renting.component.css']
})
export class RentingComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  rentings: RentingDto[] = [];
  displayedColumns: string[] = ['car', 'client', 'period', 'state', 'price', 'actions'];

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;
  state: RentingState | null = null;

  RentingState = RentingState;
  states = [
    { value: RentingState.NotYet, labelKey: 'enums.rentingState.notYet' },
    { value: RentingState.InProgress, labelKey: 'enums.rentingState.inProgress' },
    { value: RentingState.Done, labelKey: 'enums.rentingState.done' },
    { value: RentingState.Cancelled, labelKey: 'enums.rentingState.cancelled' }
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

  // Returns a transloco key; the template pipes it.
  stateLabelKey(state?: RentingState): string {
    return this.states.find(s => s.value === state)?.labelKey ?? '';
  }

  canCancel(renting: RentingDto): boolean {
    return renting.rentingState === RentingState.NotYet
      || renting.rentingState === RentingState.InProgress;
  }

  cancelRenting(renting: RentingDto) {
    if (!renting.id) return;
    if (confirm(this.transloco.translate('renting.confirmCancel'))) {
      this.client.cancelRenting(renting.id).subscribe({
        next: () => this.load(),
        error: err => console.error(err)
      });
    }
  }
}
