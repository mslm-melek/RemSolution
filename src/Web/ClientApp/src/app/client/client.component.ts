import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Sort, SortDirection } from '@angular/material/sort';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { ClientsClient, ClientDto } from '../web-api-client';
import {
  FilterChip, applyListFilters, boolParam, dateParam, rangeText, withoutParams
} from '../shared/list-filters';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-client',
  templateUrl: './client.component.html',
  styleUrls: ['./client.component.css']
})
export class ClientComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  clients: ClientDto[] = [];
  displayedColumns: string[] = ['name', 'email', 'birthDate', 'cin', 'documents', 'actions'];

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;
  search = '';

  // Sorting is server-side: the column id doubles as the API's SortBy key, and
  // the starting values mirror the query's own default order.
  sortBy = 'name';
  sortDirection: SortDirection = 'asc';

  // Filters that arrive by link (from the dashboard's client counts) and have no
  // control on the strip; they show as removable chips instead.
  flagged: boolean | null = null;
  addedFrom: Date | null = null;
  addedTo: Date | null = null;
  chips: FilterChip[] = [];

  constructor(
    private client: ClientsClient,
    private route: ActivatedRoute,
    private router: Router) { }

  // The URL holds the filters (see shared/list-filters), so the list reloads
  // whenever they change — including when the menu's plain "Clients" link clears
  // the ones a dashboard tile arrived with.
  ngOnInit() {
    this.route.queryParamMap.subscribe(params => {
      this.readFilters(params);
      this.pageNumber = 1;
      this.load();
    });
  }

  private readFilters(params: ParamMap) {
    this.search = params.get('search') ?? '';
    this.flagged = boolParam(params, 'flagged');
    this.addedFrom = dateParam(params, 'addedFrom');
    this.addedTo = dateParam(params, 'addedTo');

    this.chips = [];

    if (this.flagged !== null) {
      this.chips.push({
        params: ['flagged'],
        labelKey: this.flagged ? 'filters.flagged' : 'filters.notFlagged'
      });
    }

    if (this.addedFrom || this.addedTo) {
      this.chips.push({
        params: ['addedFrom', 'addedTo'],
        labelKey: 'filters.added',
        labelArgs: { range: rangeText(params.get('addedFrom'), params.get('addedTo')) }
      });
    }
  }

  load() {
    this.client.getClients(
      this.pageNumber, this.pageSize, this.search.trim() || null, null,
      this.flagged, this.addedFrom, this.addedTo,
      this.sortBy, this.sortDirection === 'desc'
    ).subscribe({
      next: result => {
        this.clients = result.items || [];
        this.totalCount = result.totalCount || 0;
      },
      error: err => console.error(err)
    });
  }

  // Searching goes through the URL; the subscription above reloads the rows.
  onSearch() {
    applyListFilters(this.router, this.route, {
      ...withoutParams(this.route.snapshot.queryParamMap, ['search']),
      search: this.search.trim() || null
    });
  }

  clearSearch() {
    this.search = '';
    this.onSearch();
  }

  clearChip(chip: FilterChip) {
    applyListFilters(
      this.router, this.route, withoutParams(this.route.snapshot.queryParamMap, chip.params));
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }

  // A new sort re-queries from page one: the row that was on top of page three
  // is meaningless once the order changed.
  onSort(sort: Sort) {
    this.sortBy = sort.active;
    this.sortDirection = sort.direction || 'asc';
    this.pageNumber = 1;
    this.load();
  }

  deleteClient(client: ClientDto) {
    if (!client.id) return;

    const name = `${client.firstName} ${client.lastName}`;
    if (confirm(this.transloco.translate('client.confirmDelete', { name }))) {
      this.client.deleteClient(client.id).subscribe({
        next: () => this.load(),
        error: err => console.error(err)
      });
    }
  }
}
