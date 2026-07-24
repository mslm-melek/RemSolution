import { Component, OnInit } from '@angular/core';
import { AgenciesClient, AgencyDto } from '../web-api-client';

@Component({
  selector: 'app-agency',
  templateUrl: './agency.component.html',
  styleUrls: ['./agency.component.css']
})
export class AgencyComponent implements OnInit {
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

    if (confirm(`Delete agency "${agency.name}"? This is only allowed when the agency has no cars, clients or other records.`)) {
      this.client.deleteAgency(agency.id).subscribe({
        next: () => this.load(),
        error: err => {
          // The API refuses deletion of an agency that still owns data.
          alert('This agency could not be deleted. It may still have cars, clients or other records.');
          console.error(err);
        }
      });
    }
  }
}
