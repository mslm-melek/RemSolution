import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { TodayBranchDto } from '../web-api-client';

const STORAGE_KEY = 'remsolution.branch';

/**
 * Which branch the desk is standing in.
 *
 * The picker lives in the app bar but the scope belongs to the home screen: the
 * fleet, the day's movements and the money on it are all counted for one branch
 * (a car has a home branch, and a booking is placed at its car's). Nothing else
 * in the app is branch-scoped yet, which is why the bar only draws the picker on
 * the home route.
 *
 * The list itself comes from the home screen's own payload (see GetTodayQuery),
 * so the bar needs no call of its own: home publishes what the server sent, and
 * clears it on the way out — which is what makes the picker disappear on every
 * other screen rather than sit there scoping nothing.
 *
 * The choice is remembered across reloads: the branch somebody works at is not a
 * per-visit decision.
 */
@Injectable({ providedIn: 'root' })
export class BranchScopeService {
  /** The branches the current user could pick. Empty ⇒ no picker. */
  readonly branches$ = new BehaviorSubject<TodayBranchDto[]>([]);

  /** The chosen branch, or null for "the whole agency". */
  readonly branchId$ = new BehaviorSubject<number | null>(restore());

  get branchId(): number | null {
    return this.branchId$.value;
  }

  /** Called by the screen that knows the branches — see the class remarks. */
  publish(branches: TodayBranchDto[] | undefined) {
    const list = branches ?? [];
    this.branches$.next(list);

    // A remembered branch the user can no longer see (it was deleted, or their
    // agency changed) would silently filter everything to nothing. Fall back to
    // the whole agency rather than to an empty screen.
    if (this.branchId !== null && list.length && !list.some(b => b.id === this.branchId)) {
      this.select(null);
    }
  }

  clear() {
    this.branches$.next([]);
  }

  select(branchId: number | null) {
    if (branchId === null) {
      localStorage.removeItem(STORAGE_KEY);
    } else {
      localStorage.setItem(STORAGE_KEY, String(branchId));
    }

    this.branchId$.next(branchId);
  }

  /** The chosen branch's name, for the bar's button. */
  nameOf(branchId: number | null): string | null {
    return this.branches$.value.find(b => b.id === branchId)?.name ?? null;
  }
}

function restore(): number | null {
  const raw = Number(localStorage.getItem(STORAGE_KEY));
  return Number.isInteger(raw) && raw > 0 ? raw : null;
}
