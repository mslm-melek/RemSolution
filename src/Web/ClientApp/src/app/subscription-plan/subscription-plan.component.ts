import { Component, OnInit, inject } from '@angular/core';
import { SubscriptionPlansClient, SubscriptionPlanDto } from '../web-api-client';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-subscription-plan',
  templateUrl: './subscription-plan.component.html',
  styleUrls: ['./subscription-plan.component.css']
})
export class SubscriptionPlanComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  plans: SubscriptionPlanDto[] = [];
  displayedColumns: string[] = ['name', 'maxCars', 'maxClients', 'maxUsers', 'price', 'actions'];

  constructor(private client: SubscriptionPlansClient) { }

  ngOnInit() {
    this.load();
  }

  load() {
    this.client.getSubscriptionPlans().subscribe({
      next: result => this.plans = result || [],
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
