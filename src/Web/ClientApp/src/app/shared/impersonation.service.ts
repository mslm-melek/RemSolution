import { Injectable } from '@angular/core';

// The agency a platform administrator is currently working inside.
export interface ImpersonatedAgency {
  id: number;
  name: string;
}

// What goes in the store: the agency plus the account that opened it. `owner` is
// null until the current-user probe answers, and `reconcile` fills it in.
interface StoredWorkspace extends ImpersonatedAgency {
  owner: string | null;
}

// Holds the agency workspace a platform administrator has entered; while one is
// open the interceptor stamps X-Impersonate-Agency on every tenant-scoped request.
//
// Kept in sessionStorage: per tab (two tabs can sit in two agencies) and readable
// synchronously, which the interceptor needs before the first /api/Users/me call.
// Entering and leaving reload the page, because permissions and features are
// fetched once per app load.
//
// Per tab also means it outlives a sign-out, so it is stamped with its owner and
// reconciled against the server on every load (see `reconcile`).
@Injectable({ providedIn: 'root' })
export class ImpersonationService {
  private static readonly storageKey = 'remsolution.agency-workspace';

  private workspace: StoredWorkspace | null = ImpersonationService.read();

  get current(): ImpersonatedAgency | null {
    return this.workspace ? { id: this.workspace.id, name: this.workspace.name } : null;
  }

  get currentId(): number | null {
    return this.workspace?.id ?? null;
  }

  // Opens the agency's workspace and lands on `landOn`. Already being in that
  // agency is a no-op, so a repeated click does not reload the page.
  //
  // Home is the default landing, not the dashboard: the dashboard needs the
  // Dashboard feature, which the agency's plan may exclude — 403 for the whole
  // module, platform admin included. Home works for every agency and shows what
  // this one actually has.
  enter(agency: ImpersonatedAgency, landOn = '/'): void {
    if (this.workspace?.id === agency.id) {
      window.location.assign(landOn);
      return;
    }

    this.workspace = { ...agency, owner: null };
    ImpersonationService.write(this.workspace);
    window.location.assign(landOn);
  }

  /**
   * Drops the stored workspace when it no longer belongs to whoever is signed in,
   * and reports whether it did.
   *
   * `honoured` is the server's answer about the header this load sent
   * (CurrentUserDto.IsImpersonating); false means signed out, not a platform
   * admin, or the agency is gone. The owner is compared too, since the API would
   * honour the header for a different platform admin.
   */
  reconcile(userName: string | null | undefined, honoured: boolean): boolean {
    if (!this.workspace) return false;

    if (!honoured) {
      this.discard();
      return true;
    }

    const signedIn = userName ?? null;

    // First load after entering: record whoever is signed in as the owner.
    if (this.workspace.owner === null) {
      this.workspace = { ...this.workspace, owner: signedIn };
      ImpersonationService.write(this.workspace);
      return false;
    }

    if (this.workspace.owner !== signedIn) {
      this.discard();
      return true;
    }

    return false;
  }

  // Leaves the workspace and returns to the agency's console page.
  exit(): void {
    const previous = this.workspace;

    this.discard();
    window.location.assign(previous ? `/agency/${previous.id}` : '/agency');
  }

  // Drops the workspace without navigating, for callers that navigate themselves
  // (signing out) or reload (see AuthService).
  discard(): void {
    this.workspace = null;
    ImpersonationService.write(null);
  }

  private static read(): StoredWorkspace | null {
    // Guarded: there is no sessionStorage when the app renders server-side.
    if (typeof window === 'undefined' || !window.sessionStorage) return null;

    const raw = window.sessionStorage.getItem(ImpersonationService.storageKey);
    if (!raw) return null;

    try {
      const parsed = JSON.parse(raw) as StoredWorkspace;
      if (typeof parsed?.id !== 'number') return null;

      // Values written before owners existed read as unowned, so the next load claims them.
      return { id: parsed.id, name: parsed.name, owner: parsed.owner ?? null };
    } catch {
      // Corrupt value: drop it rather than wedging every request behind it.
      window.sessionStorage.removeItem(ImpersonationService.storageKey);
      return null;
    }
  }

  private static write(workspace: StoredWorkspace | null): void {
    if (typeof window === 'undefined' || !window.sessionStorage) return;

    if (workspace) {
      window.sessionStorage.setItem(ImpersonationService.storageKey, JSON.stringify(workspace));
    } else {
      window.sessionStorage.removeItem(ImpersonationService.storageKey);
    }
  }
}
