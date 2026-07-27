import { Component, OnInit, inject } from '@angular/core';
import { AgenciesClient, AgencyDto } from '../web-api-client';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-agency',
  templateUrl: './agency.component.html',
  styleUrls: ['./agency.component.css']
})
export class AgencyComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  agencies: AgencyDto[] = [];
  displayedColumns: string[] = ['name', 'country', 'contact', 'currency', 'actions'];

  constructor(private client: AgenciesClient) { }

  ngOnInit() {
    this.load();
  }

  load() {
    // The API returns the full list (no server-side paging on agencies).
    this.client.getAgencies().subscribe({
      next: result => this.agencies = result || [],
      error: err => console.error(err)
    });
  }

  deleteAgency(agency: AgencyDto) {
    if (!agency.id) return;

    if (confirm(this.transloco.translate('agency.confirmDelete', { name: agency.name }))) {
      this.client.deleteAgency(agency.id).subscribe({
        next: () => this.load(),
        error: err => {
          // The API refuses deletion of an agency that still owns data.
          alert(this.transloco.translate('agency.deleteFailed'));
          console.error(err);
        }
      });
    }
  }
}
