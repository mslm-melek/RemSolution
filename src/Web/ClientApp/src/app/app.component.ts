import { Component, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from './shared/auth.service';

/**
 * The shell: a navigation rail beside a content column, with the app bar at the
 * top of that column.
 *
 * Signed out there is no rail — the marketplace is a public website, and a
 * visitor has nothing to navigate between. The two states share ONE
 * router-outlet: putting an outlet in each branch would re-instantiate the
 * current route the moment the auth probe answered.
 */
@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit {
  // null until the auth probe answers. Neither chrome is drawn in the meantime:
  // one request's worth of an empty bar beats flashing the wrong navigation.
  signedIn: boolean | null = null;

  // The rail is an overlay below the layout breakpoint.
  menuOpen = false;

  constructor(private auth: AuthService, private router: Router) { }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => this.signedIn = user.isAuthenticated ?? false);

    // A route change closes the overlay even when it was not a rail link that
    // caused it (a card on the home screen, the browser's back button).
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => this.menuOpen = false);
  }
}
