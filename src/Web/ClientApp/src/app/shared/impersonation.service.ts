import { Injectable } from '@angular/core';

// Holds the agency a platform administrator is currently browsing read-only.
// The impersonation interceptor reads this to stamp the X-Impersonate-Agency
// header on Cars/Clients requests, so the existing tenant-scoped endpoints
// return that agency's data. Set when entering an agency's cars/clients view,
// cleared on leave.
@Injectable({ providedIn: 'root' })
export class ImpersonationService {
  private agencyId: number | null = null;

  set(agencyId: number): void {
    this.agencyId = agencyId;
  }

  clear(): void {
    this.agencyId = null;
  }

  get current(): number | null {
    return this.agencyId;
  }
}
