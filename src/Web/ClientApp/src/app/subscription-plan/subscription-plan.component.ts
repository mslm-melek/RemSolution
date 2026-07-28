import { AfterViewInit, Component, OnInit, ViewChild, inject } from '@angular/core';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import { SubscriptionPlansClient, SubscriptionPlanDto } from '../web-api-client';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-subscription-plan',
  templateUrl: './subscription-plan.component.html',
  styleUrls: ['./subscription-plan.component.css']
})
export class SubscriptionPlanComponent implements OnInit, AfterViewInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  plans: SubscriptionPlanDto[] = [];
  dataSource = new MatTableDataSource<SubscriptionPlanDto>([]);

  @ViewChild(MatSort) sort!: MatSort;
  displayedColumns: string[] = ['name', 'maxCars', 'maxClients', 'maxUsers', 'price', 'actions'];

  constructor(private client: SubscriptionPlansClient) { }

  ngAfterViewInit() {
    this.dataSource.sort = this.sort;
  }

  ngOnInit() {
    this.load();
  }

  load() {
    this.client.getSubscriptionPlans().subscribe({
      next: result => {
        this.plans = result || [];
        this.dataSource.data = this.plans;
      },
      error: err => console.error(err)
    });
  }

  deletePlan(plan: SubscriptionPlanDto) {
    if (!plan.id) return;

    if (confirm(this.transloco.translate('plan.confirmDelete', { name: plan.name }))) {
      this.client.deleteSubscriptionPlan(plan.id).subscribe({
        next: () => this.load(),
        error: err => {
          alert(this.transloco.translate('plan.deleteFailed'));
          console.error(err);
        }
      });
    }
  }
}
