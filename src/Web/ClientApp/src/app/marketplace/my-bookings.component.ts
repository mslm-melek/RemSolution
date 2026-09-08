import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

/** Which part is showing. Lives in the URL as `?tab=`. */
export type MyBookingTab = 'reservations' | 'rentings' | 'reports';

/**
 * "Mes voyages": the customer's holds and their rentals on one screen.
 *
 * They were two pages, and being two only ever asked the customer to know which
 * of the two words applied to them. A hold becomes a rental (see
 * ConvertReservationCommand) — it is the same booking at two points of its life
 * — which is exactly the reasoning behind the agency's own Bookings screen.
 *
 * The tab is in the URL, so the old /my-reservations and /my-rentings routes can
 * redirect onto the half they used to be (see toMyBookings in app.module) and a
 * back button walks the tabs. Each half is its own component and fetches when it
 * is shown: the two lists come from different endpoints, and loading the one
 * nobody is looking at buys nothing.
 */
@Component({
  selector: 'app-my-bookings',
  templateUrl: './my-bookings.component.html',
  styleUrls: ['./my-bookings.component.css']
})
export class MyBookingsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  tab: MyBookingTab = 'reservations';

  ngOnInit() {
    // Read from the URL rather than held only in the field, so the back button
    // and a link that names a tab both land on it.
    this.route.queryParamMap.subscribe(params => {
      const tab = params.get('tab');
      this.tab = tab === 'rentings' || tab === 'reports' ? tab : 'reservations';
    });
  }

  switchTab(tab: MyBookingTab) {
    if (this.tab === tab) return;

    this.router.navigate([], { relativeTo: this.route, queryParams: { tab } });
  }
}
