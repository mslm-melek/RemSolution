import { AfterViewInit, Component, OnInit, ViewChild, inject } from '@angular/core';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import { AgenciesClient, AgencyDto } from '../web-api-client';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-agency',
  templateUrl: './agency.component.html',
  styleUrls: ['./agency.component.css']
})
export class AgencyComponent implements OnInit, AfterViewInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  agencies: AgencyDto[] = [];
  dataSource = new MatTableDataSource<AgencyDto>([]);
  displayedColumns: string[] = ['name', 'country', 'contact', 'currency', 'actions'];

  @ViewChild(MatSort) sort!: MatSort;

  constructor(private client: AgenciesClient) {
    // Columns whose id is not the property name need to say what they sort on.
    this.dataSource.sortingDataAccessor = (agency, column) => {
      switch (column) {
        case 'country': return agency.countryName ?? '';
        case 'contact': return agency.email ?? agency.phoneNumber ?? '';
        case 'currency': return agency.currency ?? '';
        default: return agency.name ?? '';
      }
    };
  }

  ngAfterViewInit() {
    this.dataSource.sort = this.sort;
  }

  ngOnInit() {
    this.load();
  }

  load() {
    // The API returns the full list (no server-side paging on agencies).
    this.client.getAgencies().subscribe({
      next: result => {
        this.agencies = result || [];
        this.dataSource.data = this.agencies;
      },
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
