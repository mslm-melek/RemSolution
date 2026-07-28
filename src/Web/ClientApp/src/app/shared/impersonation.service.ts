import { Injectable } from '@angular/core';

// The agency a platform administrator is currently working inside.
export interface ImpersonatedAgency {
  id: number;
  name: string;
}

// Holds the agency workspace a platform administrator has entered. While one is
// open, the impersonation interceptor stamps X-Impersonate-Agency on every
// tenant-scoped request, so the ordinary agency screens read and write that
// agency's data — the API grants the agency's permissions for the duration of
// each such request and records it.
//
// The state is per session rather than per screen: entering an agency changes
// what the whole app is looking at, so it has to survive navigation and the page
// reloads the app already does (switching language reloads). sessionStorage is
// the right store for it — per tab, so two tabs can sit in two different
// agencies, and readable synchronously, which matters because the interceptor
// needs it before the very first /api/Users/me call.
//
// Entering and leaving reload the page on purpose: the signed-in user's
// permissions and enabled features are fetched once per app load, and they
// change completely with the agency context.
@Injectable({ providedIn: 'root' })
export class ImpersonationService {
  private static readonly storageKey = 'remsolution.agency-workspace';

  private agency: ImpersonatedAgency | null = ImpersonationService.read();

  get current(): ImpersonatedAgency | null {
    return this.agency;
  }

  get currentId(): number | null {
    return this.agency?.id ?? null;
  }

  // Opens the agency's workspace and lands on `landOn`. Already being in that
  // agency is a no-op, so a repeated click does not reload the page.
  enter(agency: ImpersonatedAgency, landOn = '/dashboard'): void {
    if (this.agency?.id === agency.id) {
      window.location.assign(landOn);
      return;
    }

    this.agency = agency;
    ImpersonationService.write(agency);
    window.location.assign(landOn);
  }

  // Leaves the workspace and returns to the agency's console page.
  exit(): void {
    const previous = this.agency;

    this.discard();
    window.location.assign(previous ? `/agency/${previous.id}` : '/agency');
  }

  // Drops the workspace without navigating. For the caller that has to undo a
  // workspace the server will not honour (see AuthService), where a navigation
  // would fight the reload it is about to do anyway.
  discard(): void {
    this.agency = null;
    ImpersonationService.write(null);
  }

  private static read(): ImpersonatedAgency | null {
    // Guarded rather than assumed: the app is also rendered server-side, where
    // there is no sessionStorage (and no impersonation either).
    if (typeof window === 'undefined' || !window.sessionStorage) return null;

    const raw = window.sessionStorage.getItem(ImpersonationService.storageKey);
    if (!raw) return null;

    try {
      const parsed = JSON.parse(raw) as ImpersonatedAgency;
      return typeof parsed?.id === 'number' ? parsed : null;
    } catch {
      // Corrupt value: drop it rather than wedging every request behind it.
      window.sessionStorage.removeItem(ImpersonationService.storageKey);
      return null;
    }
  }

  private static write(agency: ImpersonatedAgency | null): void {
    if (typeof window === 'undefined' || !window.sessionStorage) return;

    if (agency) {
      window.sessionStorage.setItem(ImpersonationService.storageKey, JSON.stringify(agency));
    } else {
      window.sessionStorage.removeItem(ImpersonationService.storageKey);
    }
  }
}
