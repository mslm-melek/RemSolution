import { Component, OnDestroy, OnInit } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { ActivatedRoute } from '@angular/router';
import { ClientsClient, ClientDto } from '../web-api-client';
import { ImpersonationService } from '../shared/impersonation.service';

// Read-only browse of an agency's clients for a platform admin (see AgencyCarsComponent).
@Component({
  selector: 'app-agency-clients',
  templateUrl: './agency-clients.component.html',
  styleUrls: ['./agency.component.css']
})
export class AgencyClientsComponent implements OnInit, OnDestroy {
  agencyId!: number;
  clients: ClientDto[] = [];
  displayedColumns: string[] = ['name', 'birthDate', 'cin'];

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;

  constructor(
    private route: ActivatedRoute,
    private client: ClientsClient,
    private impersonation: ImpersonationService
  ) { }

  ngOnInit() {
    this.agencyId = +this.route.snapshot.paramMap.get('id')!;
    this.impersonation.set(this.agencyId);
    this.load();
  }

  ngOnDestroy() {
    this.impersonation.clear();
  }

  load() {
    this.client.getClients(this.pageNumber, this.pageSize, null, null).subscribe({
      next: result => {
        this.clients = result.items || [];
        this.totalCount = result.totalCount || 0;
      },
      error: err => console.error(err)
    });
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }
}
