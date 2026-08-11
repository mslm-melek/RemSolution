import { Component, OnInit, inject } from '@angular/core';
import { Directionality } from '@angular/cdk/bidi';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { SortDirection } from '@angular/material/sort';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { ClientsClient, ClientDto, CreditsClient, ClientCreditDto } from '../web-api-client';
import {
  FilterChip, applyListFilters, boolParam, dateParam, rangeText, withoutParams
} from '../shared/list-filters';
import { AuthService } from '../shared/auth.service';
import { LateNoticeService } from '../shared/late-notice.service';
import {
  ClientStanding, clientStanding, clientStandingClass, clientStandingIcon, clientStandingLabelKey
} from '../shared/client-standing';
import {
  ClientQuickViewComponent, ClientQuickViewData, ClientQuickViewResult
} from './client-quick-view.component';

/** One button on the standing strip. `all` is "no standing filter at all". */
export type ClientPill = 'all' | ClientStanding;

/** A client as the card draws them: the record plus what was worked out about it. */
interface ClientRow {
  client: ClientDto;
  name: string;
  standing: ClientStanding;
  standingLabelKey: string;
  standingClass: string;
  standingIcon: string;
  /** A car out past its return date — the one thing on a row that is overdue. */
  late: boolean;
}

@Component({
  selector: 'app-client',
  templateUrl: './client.component.html',
  styleUrls: ['./client.component.css']
})
export class ClientComponent implements OnInit {
  private readonly dialog = inject(MatDialog);
  // The panel is pinned to the edge the page ends on, which is the other edge in
  // Arabic. A dialog is positioned in absolute terms (the CDK overlay knows
  // nothing about the page's direction), so the side is chosen here rather than
  // left to a logical property in the stylesheet.
  private readonly direction = inject(Directionality);

  clients: ClientDto[] = [];
  /** What the cards are drawn from; rebuilt whenever a page or its debts land. */
  rows: ClientRow[] = [];

  // What each client on this page owes, by client id. Money is the Credits
  // module's answer and stays behind its permission, so it is asked for
  // separately (see GetClientCreditsByIdsQuery) rather than riding on ClientDto.
  credits: Record<number, ClientCreditDto> = {};
  canSeeCredit = false;
  canSeeRentings = false;
  canRent = false;
  // Writing to a customer in the agency's name is its own grant, so the action is
  // offered only to someone who holds it.
  canRemind = false;

  // What the last late notice did, shown as a banner. Held here rather than in an
  // alert() because "already sent today" is information, not an interruption.
  noticeMessage = '';

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;
  search = '';

  // The cards have no headers to click, so the order lives in the toolbar's menu.
  // It is still the server's order (the key doubles as the API's SortBy), not a
  // reshuffle of the page already on screen.
  sortBy = 'name';
  sortDirection: SortDirection = 'asc';
  readonly sortOptions = [
    { key: 'name', labelKey: 'common.name' },
    { key: 'cin', labelKey: 'client.cin' },
    { key: 'birthDate', labelKey: 'client.birthDate' },
    { key: 'rentings', labelKey: 'client.rentings' }
  ];

  // --- Filters --------------------------------------------------------------
  // The standing strip writes `flagged` and `docs`; both are read back from the
  // URL, so a dashboard tile that links in with `?flagged=true` arrives with the
  // Flagged button already pressed rather than with a chip nobody asked for.
  flagged: boolean | null = null;
  documentsComplete: boolean | null = null;
  /** Which button is pressed, or null when the URL says something no button does. */
  pill: ClientPill | null = 'all';
  addedFrom: Date | null = null;
  addedTo: Date | null = null;
  chips: FilterChip[] = [];

  constructor(
    private client: ClientsClient,
    private creditsClient: CreditsClient,
    private lateNotice: LateNoticeService,
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
      this.canRemind = AuthService.canAccessModule(user, 'Notifications', 'Notification.Send');

      // The permissions arrive from a separate fetch, so a page already on
      // screen gets its debt figures filled in once they do.
      if (this.canSeeCredit && this.clients.length) this.loadCredits();
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

    const docs = params.get('docs');
    this.documentsComplete = docs === 'complete' ? true : docs === 'missing' ? false : null;

    this.pill = this.readPill();

    this.chips = [];

    // The flag has a button of its own, so it only becomes a chip when the URL
    // asks for something no button stands for — "not flagged" on its own, which
    // is neither Verified nor All.
    if (this.flagged !== null && this.pill === null) {
      this.chips.push({
        params: ['flagged', 'docs'],
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

  /** Which standing button the URL's filters add up to, if any. */
  private readPill(): ClientPill | null {
    if (this.flagged === true) return 'flagged';
    if (this.flagged !== false) return this.documentsComplete === null ? 'all' : null;
    if (this.documentsComplete === true) return 'verified';
    if (this.documentsComplete === false) return 'pending';
    return null;
  }

  load() {
    this.client.getClients(
      this.pageNumber, this.pageSize, this.search.trim() || null, null,
      this.flagged, this.documentsComplete, this.addedFrom, this.addedTo,
      this.sortBy, this.sortDirection === 'desc'
    ).subscribe({
      next: result => {
        this.clients = result.items || [];
        this.totalCount = result.totalCount || 0;
        this.credits = {};
        this.buildRows();
        if (this.canSeeCredit) this.loadCredits();
      },
      error: err => console.error(err)
    });
  }

  // Worked out once per page rather than in the template: a card binds five
  // derived values, and a getter that recomputes them on every change-detection
  // pass would do it for every card on every tick.
  private buildRows() {
    this.rows = this.clients.map(client => {
      const standing = clientStanding(client);

      return {
        client,
        name: `${client.firstName ?? ''} ${client.lastName ?? ''}`.trim(),
        standing,
        standingLabelKey: clientStandingLabelKey(standing),
        standingClass: clientStandingClass(standing),
        standingIcon: clientStandingIcon(standing),
        late: (client.overdueRentingCount ?? 0) > 0
      };
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
      // A missing debt figure is not worth an error banner over the list itself.
      error: err => console.error(err)
    });
  }

  /** What the client owes, or null while unknown / when they owe nothing. */
  outstanding(client: ClientDto): ClientCreditDto | null {
    const row = client.id ? this.credits[client.id] : undefined;
    return row && (row.outstanding?.amount ?? 0) > 0 ? row : null;
  }

  // --- Filtering ------------------------------------------------------------
  // Every control writes the URL; the subscription above reloads the rows.

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

  selectPill(pill: ClientPill) {
    const kept = withoutParams(this.route.snapshot.queryParamMap, ['flagged', 'docs']);

    switch (pill) {
      case 'flagged':
        applyListFilters(this.router, this.route, { ...kept, flagged: 'true' });
        break;
      case 'verified':
        applyListFilters(this.router, this.route, { ...kept, flagged: 'false', docs: 'complete' });
        break;
      case 'pending':
        // Not flagged as well: a flagged client is answered by the button beside
        // this one, and showing them under "papers missing" too would put the
        // same row in two places on a strip that reads as one choice.
        applyListFilters(this.router, this.route, { ...kept, flagged: 'false', docs: 'missing' });
        break;
      default:
        applyListFilters(this.router, this.route, kept);
    }
  }

  clearChip(chip: FilterChip) {
    applyListFilters(
      this.router, this.route, withoutParams(this.route.snapshot.queryParamMap, chip.params));
  }

  // --- Order and paging -----------------------------------------------------

  get activeSortLabelKey(): string {
    return this.sortOptions.find(option => option.key === this.sortBy)?.labelKey ?? 'common.name';
  }

  /** The same column again turns the order around; a new one starts ascending. */
  sortByKey(key: string) {
    if (this.sortBy === key) {
      this.sortDirection = this.sortDirection === 'desc' ? 'asc' : 'desc';
    } else {
      this.sortBy = key;
      this.sortDirection = 'asc';
    }

    this.pageNumber = 1;
    this.load();
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }

  // --- The panel ------------------------------------------------------------

  /** The client's file in short, beside the list they were clicked in. */
  open(row: ClientRow) {
    if (!row.client.id) return;

    const data: ClientQuickViewData = { id: row.client.id };

    this.dialog.open<ClientQuickViewComponent, ClientQuickViewData, ClientQuickViewResult>(
      ClientQuickViewComponent, {
        data,
        panelClass: 'side-panel',
        position: this.direction.value === 'rtl' ? { top: '0', left: '0' } : { top: '0', right: '0' },
        height: '100vh',
        width: '440px',
        maxWidth: '100vw',
        autoFocus: 'first-tabbable',
        // The panel closes itself on Escape and on a backdrop click, so that it
        // can report what happened while it was open; Material's own handling
        // would close with no result and the list would keep the old standing.
        disableClose: true
      }).afterClosed().subscribe(result => {
        if (result?.noticeMessage) this.noticeMessage = result.noticeMessage;
        if (result?.changed) this.load();
      });
  }

  // --- Row actions ----------------------------------------------------------
  // The two worth doing without opening the client at all; the rest are in the
  // panel.

  // No renting id: the command writes about the client's most overdue hire, which
  // from a list row is the only sensible choice (see SendClientLateNoticeCommand).
  remindLate(row: ClientRow) {
    if (!row.client.id) return;

    this.lateNotice.confirmAndSend(row.name, row.client.id).subscribe(message => {
      if (message) this.noticeMessage = message;
    });
  }
}
