import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Sort, SortDirection } from '@angular/material/sort';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { Observable, map } from 'rxjs';
import {
  CreditsClient, ClientCreditDto, ExpenseCreditDto, CreditsSummaryDto, MoneyDto,
  ExpensesClient, ExpenseDto, CarsClient, CarDto, ExpenseTypesClient, ExpenseTypeDto,
  FileParameter, PaginatedListOfExpenseDto, PaginatedListOfExpenseCreditDto
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { applyListFilters, boolParam } from '../shared/list-filters';
import { AuthService } from '../shared/auth.service';
import { PaymentDialogComponent, PaymentDialogData } from '../shared/payment-dialog.component';
import { TranslocoService } from '@jsverse/transloco';

// One expense row as the payable tab renders it. Two queries can fill it (see
// loadExpenses): the expense register for whoever manages expenses, and the
// credits projection for whoever only has the debt overview.
interface PayableRow {
  expenseId: number;
  expenseDate?: Date;
  carMatricule?: string;
  expenseTypeName?: string;
  amount?: MoneyDto;
  paid?: MoneyDto;
  outstanding?: MoneyDto;
  factureFileUrl?: string;
  factureFileName?: string;
}

// The figures every payable row shows; the invoice and the row actions are
// appended for whoever holds the expense module (see ngOnInit).
const PAYABLE_COLUMNS = ['date', 'car', 'type', 'amount', 'paid', 'outstanding'];

// Which side of the screen is open. It is in the URL like the filters are: a
// link that narrows the payable rows ("this car's expenses") has to land on the
// tab holding them, or the filter it carried is invisible behind the other tab.
type CreditTab = 'clients' | 'expenses';

// The agency's money screen: what clients owe coming in, what the agency owes
// going out, and the actions that settle either side. The payable tab absorbed
// the standalone expense list — it showed the same rows with the same columns —
// so booking, settling and deleting an expense all happen here now.
@Component({
  selector: 'app-credit',
  templateUrl: './credit.component.html',
  styleUrls: ['./credit.component.css']
})
export class CreditComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  private readonly dialog = inject(MatDialog);
  summary?: CreditsSummaryDto;
  errorMessage = '';

  // Receivable side: what clients owe the agency.
  clients: ClientCreditDto[] = [];
  clientColumns = ['name', 'cin', 'openRentings', 'charged', 'paid', 'outstanding', 'actions'];
  clientTotal = 0;
  clientPage = 1;
  clientPageSize = 10;
  clientOnlyOutstanding = true;
  search = '';
  // Both tables sort server-side; the defaults mirror each query's own order
  // (biggest debt first).
  clientSortBy = 'outstanding';
  clientSortDirection: SortDirection = 'desc';

  // Payable side: what the agency owes on its expenses.
  expenses: PayableRow[] = [];
  expenseColumns: string[] = [...PAYABLE_COLUMNS];
  expenseTotal = 0;
  expensePage = 1;
  expensePageSize = 10;
  // The tab is the expense register now, so it opens on every expense — the home
  // tile counting unsettled ones links with `unpaid=true` to narrow it.
  expenseOnlyOutstanding = false;
  expenseSortBy = 'outstanding';
  expenseSortDirection: SortDirection = 'desc';
  // Filters the expense list used to own; kept so nothing was lost in the move.
  carId: number | null = null;
  expenseTypeId: number | null = null;
  cars: CarDto[] = [];
  types: ExpenseTypeDto[] = [];
  // Expense id whose invoice is currently uploading, so only its own button
  // shows the pending state.
  uploadingFactureFor: number | null = null;

  // The two sides are entitled separately: Credits/Credit.Read covers the debt
  // overview (summary tiles and the client tab), Expenses/Expense.Read the
  // expense register. Either one alone still gets a usable payable tab.
  canReadCredits = false;
  canReadExpenses = false;
  // Taking money in is a payment; settling an expense is an update of its
  // running paid total (matching each API's own permission).
  canPay = false;
  canCreateExpense = false;
  canUpdateExpense = false;
  canDeleteExpense = false;
  // Which of the two queries can answer the payable tab is only known once the
  // permissions have arrived, so the first load waits for them.
  private permissionsKnown = false;

  // The open tab, from the URL (see readFilters).
  selectedTab: CreditTab = 'clients';
  // The payable filters as the URL last had them, so a tab switch — which also
  // goes through the URL — is not mistaken for a filter change and does not
  // re-query the rows or throw the user back to page one.
  private payableFilterKey: string | null = null;

  constructor(
    private client: CreditsClient,
    private expensesClient: ExpensesClient,
    private carsClient: CarsClient,
    private typesClient: ExpenseTypesClient,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router) { }

  ngOnInit() {
    // The URL is read before the permissions arrive, so the first load — fired
    // from the permission subscription below, since it is what decides which
    // query answers it — already carries the filters the link asked for.
    // The payable filters live in the URL (see shared/list-filters), so the home
    // tile counting unsettled expenses still opens this tab showing those.
    this.route.queryParamMap.subscribe(params => {
      this.readFilters(params);

      // Switching tabs rewrites the URL too; only a changed filter is worth a
      // round-trip.
      const key = `${this.carId}|${this.expenseTypeId}|${this.expenseOnlyOutstanding}`;
      if (key === this.payableFilterKey) return;

      this.payableFilterKey = key;
      this.expensePage = 1;
      if (this.permissionsKnown) this.loadExpenses();
    });

    this.auth.currentUser$.subscribe(user => {
      this.canReadCredits = AuthService.canAccessModule(user, 'Credits', 'Credit.Read');
      this.canReadExpenses = AuthService.canAccessModule(user, 'Expenses', 'Expense.Read');
      this.canPay = AuthService.canAccessModule(user, 'Payments', 'Payment.Create');
      this.canCreateExpense = AuthService.canAccessModule(user, 'Expenses', 'Expense.Create');
      this.canUpdateExpense = AuthService.canAccessModule(user, 'Expenses', 'Expense.Update');
      this.canDeleteExpense = AuthService.canAccessModule(user, 'Expenses', 'Expense.Delete');
      this.permissionsKnown = true;

      // Held in a field rather than computed per change-detection pass: the table
      // diffs its columns by reference and would re-render the header each cycle.
      this.expenseColumns = this.canReadExpenses
        ? [...PAYABLE_COLUMNS, 'facture', 'actions']
        : [...PAYABLE_COLUMNS];

      if (this.canReadCredits) {
        this.loadSummary();
        this.loadClients();
      }

      this.loadExpenses();
    });

    // Filter pickers: one page big enough to hold an agency's fleet/catalog.
    this.carsClient.getCars(1, 1000, null, null, null, null, null, null, null, null, null, null, null, false).subscribe({
      next: result => this.cars = result.items || [],
      error: err => console.error(err)
    });
    this.typesClient.getExpenseTypes(false).subscribe({
      next: types => this.types = types || [],
      error: err => console.error(err)
    });
  }

  private readFilters(params: ParamMap) {
    const carId = Number(params.get('car'));
    this.carId = Number.isInteger(carId) && carId > 0 ? carId : null;

    const typeId = Number(params.get('type'));
    this.expenseTypeId = Number.isInteger(typeId) && typeId > 0 ? typeId : null;

    this.expenseOnlyOutstanding = boolParam(params, 'unpaid') === true;

    // `?tab=` is the explicit answer. Without it, a URL carrying a payable
    // filter still opens the payable tab: those params only mean anything there,
    // so a link that has one is asking for it (and links written before the tab
    // was named keep working).
    const tab = params.get('tab');
    this.selectedTab = tab === 'expenses' || tab === 'clients'
      ? tab
      : this.carId || this.expenseTypeId || this.expenseOnlyOutstanding ? 'expenses' : 'clients';
  }

  // Which tabs are on screen, in order — mirroring their *ngIf. The receivable
  // one is missing for whoever cannot read credits, so neither tab sits at a
  // fixed index and the mapping has to be computed both ways.
  private get tabs(): CreditTab[] {
    const tabs: CreditTab[] = [];
    if (this.canReadCredits) tabs.push('clients');
    if (this.canReadExpenses || this.canReadCredits) tabs.push('expenses');
    return tabs;
  }

  get selectedTabIndex(): number {
    const index = this.tabs.indexOf(this.selectedTab);
    return index < 0 ? 0 : index;
  }

  // Keeps the open tab in the URL, so a reload, a shared link and the back
  // button all show the side the user was on. Filters are preserved: the tab is
  // one of them as far as the URL is concerned.
  onTabChange(index: number) {
    // Before the permissions land, the tab list is not final and an index means
    // nothing; the group emits on its own binding's changes then.
    if (!this.permissionsKnown) return;

    const tab = this.tabs[index];

    // Only a switch the user made differs from what the URL already says.
    if (!tab || tab === this.selectedTab) return;

    this.selectedTab = tab;
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  loadSummary() {
    this.client.getCreditsSummary().subscribe({
      next: summary => this.summary = summary,
      error: err => this.handleError(err)
    });
  }

  loadClients() {
    this.client.getClientCredits(
      this.clientPage, this.clientPageSize, this.clientOnlyOutstanding, this.search || null,
      this.clientSortBy, this.clientSortDirection === 'desc'
    ).subscribe({
      next: result => {
        this.clients = result.items || [];
        this.clientTotal = result.totalCount || 0;
      },
      error: err => this.handleError(err)
    });
  }

  // Reads the register when the user has the expense module (it carries the
  // invoice and drives the row actions), and the credits projection otherwise —
  // the debt overview stays grantable on its own, as it was before the merge.
  loadExpenses() {
    if (!this.canReadExpenses && !this.canReadCredits) return;

    const rows: Observable<{ items: PayableRow[]; totalCount: number }> = this.canReadExpenses
      ? this.expensesClient.getExpenses(
          this.expensePage, this.expensePageSize, this.carId, this.expenseTypeId,
          null, null, this.expenseOnlyOutstanding,
          this.expenseSortBy, this.expenseSortDirection === 'desc'
        ).pipe(map((result: PaginatedListOfExpenseDto) => ({
          items: (result.items || []).map(fromExpense),
          totalCount: result.totalCount || 0
        })))
      : this.client.getExpenseCredits(
          this.expensePage, this.expensePageSize, this.expenseOnlyOutstanding,
          this.carId, this.expenseTypeId,
          this.expenseSortBy, this.expenseSortDirection === 'desc'
        ).pipe(map((result: PaginatedListOfExpenseCreditDto) => ({
          items: (result.items || []).map(fromExpenseCredit),
          totalCount: result.totalCount || 0
        })));

    rows.subscribe({
      next: result => {
        this.expenses = result.items;
        this.expenseTotal = result.totalCount;
      },
      error: err => this.handleError(err)
    });
  }

  onClientFilter() {
    this.clientPage = 1;
    this.loadClients();
  }

  onClientPage(event: PageEvent) {
    this.clientPage = event.pageIndex + 1;
    this.clientPageSize = event.pageSize;
    this.loadClients();
  }

  // Payable filtering goes through the URL; the subscription above reloads it.
  // The whole query string is replaced, so the open tab has to be restated or
  // filtering would drop the user back onto the receivable side.
  onExpenseFilter() {
    applyListFilters(this.router, this.route, {
      tab: 'expenses',
      car: this.carId,
      type: this.expenseTypeId,
      unpaid: this.expenseOnlyOutstanding ? 'true' : null
    });
  }

  clearExpenseFilters() {
    applyListFilters(this.router, this.route, { tab: 'expenses' });
  }

  onExpensePage(event: PageEvent) {
    this.expensePage = event.pageIndex + 1;
    this.expensePageSize = event.pageSize;
    this.loadExpenses();
  }

  onClientSort(sort: Sort) {
    this.clientSortBy = sort.active;
    this.clientSortDirection = sort.direction || 'asc';
    this.clientPage = 1;
    this.loadClients();
  }

  onExpenseSort(sort: Sort) {
    this.expenseSortBy = sort.active;
    this.expenseSortDirection = sort.direction || 'asc';
    this.expensePage = 1;
    this.loadExpenses();
  }

  clearSearch() {
    this.search = '';
    this.onClientFilter();
  }

  carLabel(car: CarDto): string {
    return car.modelName ? `${car.matricule} — ${car.modelName}` : (car.matricule ?? '');
  }

  isSettled(row: PayableRow): boolean {
    return (row.outstanding?.amount ?? 0) <= 0;
  }

  // Takes money from a client without opening any of their bookings first: a
  // standalone client payment, which is what a counter payment against an
  // overall balance actually is.
  payClient(row: ClientCreditDto) {
    if (!row.clientId) return;

    this.openMoneyDialog({
      target: { kind: 'client', id: row.clientId },
      subtitle: row.clientName ?? undefined,
      outstanding: row.outstanding?.amount,
      currency: row.outstanding?.currency ?? this.summary?.currency
    }, () => {
      this.loadSummary();
      this.loadClients();
    });
  }

  // Settles part or all of an expense. The API takes a delta and enforces the
  // ceiling, so the dialog offers the outstanding amount and the server has the
  // last word.
  settleExpense(row: PayableRow) {
    if (!row.expenseId) return;

    this.openMoneyDialog({
      target: { kind: 'expense', id: row.expenseId },
      subtitle: [row.carMatricule, row.expenseTypeName].filter(Boolean).join(' — '),
      outstanding: row.outstanding?.amount,
      currency: row.outstanding?.currency ?? this.summary?.currency
    }, () => {
      if (this.canReadCredits) this.loadSummary();
      this.loadExpenses();
    });
  }

  private openMoneyDialog(data: PaymentDialogData, reload: () => void) {
    this.errorMessage = '';

    this.dialog.open(PaymentDialogComponent, { data, autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe(recorded => {
        if (recorded) reload();
      });
  }

  // The supplier invoice behind the expense. Attached from the row because that
  // is where the unpaid expense is being looked at.
  onFactureSelected(row: PayableRow, input: HTMLInputElement) {
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file
    if (!file || !row.expenseId) return;

    this.uploadingFactureFor = row.expenseId;
    this.errorMessage = '';
    const parameter: FileParameter = { data: file, fileName: file.name };

    this.expensesClient.uploadExpenseFacture(row.expenseId, parameter).subscribe({
      next: () => {
        this.uploadingFactureFor = null;
        this.loadExpenses();
      },
      error: err => {
        this.uploadingFactureFor = null;
        this.handleError(err);
      }
    });
  }

  deleteExpense(row: PayableRow) {
    if (!row.expenseId) return;
    if (!confirm(this.transloco.translate('expense.confirmDelete'))) return;

    this.expensesClient.deleteExpense(row.expenseId).subscribe({
      next: () => {
        if (this.canReadCredits) this.loadSummary();
        this.loadExpenses();
      },
      error: err => this.handleError(err)
    });
  }

  private handleError(err: any) {
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}

// Both projections already expose the same figures under their own names; the
// column ids the table sorts by are shared, so only the shape differs.
function fromExpense(dto: ExpenseDto): PayableRow {
  return {
    expenseId: dto.id!,
    expenseDate: dto.expenseDate,
    carMatricule: dto.carMatricule,
    expenseTypeName: dto.expenseTypeName,
    amount: dto.expenseAmount,
    paid: dto.paidAmount,
    outstanding: dto.outstanding,
    factureFileUrl: dto.factureFileUrl,
    factureFileName: dto.factureFileName
  };
}

function fromExpenseCredit(dto: ExpenseCreditDto): PayableRow {
  return {
    expenseId: dto.expenseId!,
    expenseDate: dto.expenseDate,
    carMatricule: dto.carMatricule,
    expenseTypeName: dto.expenseTypeName,
    amount: dto.amount,
    paid: dto.paid,
    outstanding: dto.outstanding,
    factureFileUrl: dto.factureFileUrl,
    factureFileName: dto.factureFileName
  };
}
