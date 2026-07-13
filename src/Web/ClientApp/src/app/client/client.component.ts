import { Component, OnInit } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { ClientsClient, ClientDto } from '../web-api-client';

@Component({
  selector: 'app-client',
  templateUrl: './client.component.html',
  styleUrls: ['./client.component.css']
})
export class ClientComponent implements OnInit {
  clients: ClientDto[] = [];
  displayedColumns: string[] = ['name', 'birthDate', 'cin', 'documents', 'actions'];

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;
  search = '';

  constructor(private client: ClientsClient) { }

  ngOnInit() {
    this.load();
  }

  load() {
    this.client.getClients(this.pageNumber, this.pageSize, this.search.trim() || null, null).subscribe({
      next: result => {
        this.clients = result.items || [];
        this.totalCount = result.totalCount || 0;
      },
      error: err => console.error(err)
    });
  }

  onSearch() {
    this.pageNumber = 1;
    this.load();
  }

  clearSearch() {
    this.search = '';
    this.onSearch();
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }

  deleteClient(client: ClientDto) {
    if (!client.id) return;

    if (confirm(`Delete client "${client.firstName} ${client.lastName}"? Their uploaded documents will also be removed.`)) {
      this.client.deleteClient(client.id).subscribe({
        next: () => this.load(),
        error: err => console.error(err)
      });
    }
  }
}
