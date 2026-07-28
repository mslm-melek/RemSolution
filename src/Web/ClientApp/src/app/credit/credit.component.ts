import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Sort, SortDirection } from '@angular/material/sort';
import {
  CreditsClient, ClientCreditDto, ExpenseCreditDto, CreditsSummaryDto
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-credit',
  templateUrl: './credit.component.html',
  styleUrls: ['./credit.component.css']
})
export class CreditComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  summary?: CreditsSummaryDto;
  errorMessage = '';

  // Receivable side: what clients owe the agency.
  clients: ClientCreditDto[] = [];
  clientColumns = ['name', 'cin', 'openRentings', 'charged', 'paid', 'outstanding'];
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
  expenses: ExpenseCreditDto[] = [];
  expenseColumns = ['date', 'car', 'type', 'amount', 'paid', 'outstanding'];
  expenseTotal = 0;
  expensePage = 1;
  expensePageSize = 10;
  expenseOnlyOutstanding = true;
  expenseSortBy = 'outstanding';
  expenseSortDirection: SortDirection = 'desc';

  constructor(private client: CreditsClient) { }

  ngOnInit() {
    this.loadSummary();
    this.loadClients();
    this.loadExpenses();
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

  loadExpenses() {
    this.client.getExpenseCredits(
      this.expensePage, this.expensePageSize, this.expenseOnlyOutstanding, null,
      this.expenseSortBy, this.expenseSortDirection === 'desc'
    ).subscribe({
      next: result => {
        this.expenses = result.items || [];
        this.expenseTotal = result.totalCount || 0;
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

  onExpenseFilter() {
    this.expensePage = 1;
    this.loadExpenses();
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

  private handleError(err: any) {
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}
