import { Component, OnInit } from '@angular/core';
import { SubscriptionPlansClient, SubscriptionPlanDto } from '../web-api-client';

@Component({
  selector: 'app-subscription-plan',
  templateUrl: './subscription-plan.component.html',
  styleUrls: ['./subscription-plan.component.css']
})
export class SubscriptionPlanComponent implements OnInit {
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

    if (confirm(`Delete plan "${plan.name}"? This is only allowed when no agency uses it.`)) {
      this.client.deleteSubscriptionPlan(plan.id).subscribe({
        next: () => this.load(),
        error: err => {
          alert('This plan could not be deleted. It may still be assigned to an agency.');
          console.error(err);
        }
      });
    }
  }
}
