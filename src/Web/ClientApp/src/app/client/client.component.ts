import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Sort, SortDirection } from '@angular/material/sort';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { ClientsClient, ClientDto, CreditsClient, ClientCreditDto } from '../web-api-client';
import {
  FilterChip, applyListFilters, boolParam, dateParam, rangeText, withoutParams
} from '../shared/list-filters';
import { AuthService } from '../shared/auth.service';
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
  // The debt column only exists for someone allowed to see debt, so the table
  // does not show an empty column to everyone else.
  displayedColumns: string[] = ['name', 'email', 'birthDate', 'cin', 'rentings', 'documents', 'actions'];

  // What each client on this page owes, by client id. Money is the Credits
  // module's answer and stays behind its permission, so it is asked for
  // separately (see GetClientCreditsByIdsQuery) rather than riding on ClientDto.
  credits: Record<number, ClientCreditDto> = {};
  canSeeCredit = false;
  canSeeRentings = false;
  canRent = false;

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
    private creditsClient: CreditsClient,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router) { }

  // The URL holds the filters (see shared/list-filters), so the list reloads
  // whenever they change — including when the menu's plain "Clients" link clears
  // the ones a dashboard tile arrived with.
  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.canSeeCredit = AuthService.canAccessModule(user, 'Credits', 'Credit.Read');
      this.canSeeRentings = AuthService.canAccessModule(user, 'Rentings', 'Renting.Read');
      this.canRent = AuthService.canAccessModule(user, 'Rentings', 'Renting.Create');

      if (this.canSeeCredit) {
        this.displayedColumns = [
          'name', 'email', 'birthDate', 'cin', 'rentings', 'credit', 'documents', 'actions'
        ];
        // The permissions arrive from a separate fetch, so a page already on
        // screen gets its debt column filled in once they do.
        if (this.clients.length) this.loadCredits();
      }
    });

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
        this.credits = {};
        if (this.canSeeCredit) this.loadCredits();
      },
      error: err => console.error(err)
    });
  }

  // One call for the whole page rather than one per row.
  private loadCredits() {
    const ids = this.clients.map(c => c.id).filter((id): id is number => !!id);
    if (!ids.length) return;

    this.creditsClient.getClientCreditsByIds(ids).subscribe({
      next: rows => {
        const byId: Record<number, ClientCreditDto> = {};
        for (const row of rows || []) {
          if (row.clientId) byId[row.clientId] = row;
        }
        this.credits = byId;
      },
      // A missing debt column is not worth an error banner over the list itself.
      error: err => console.error(err)
    });
  }

  /** What the client owes, or null while unknown / when they owe nothing. */
  outstanding(client: ClientDto): ClientCreditDto | null {
    const row = client.id ? this.credits[client.id] : undefined;
    return row && (row.outstanding?.amount ?? 0) > 0 ? row : null;
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
