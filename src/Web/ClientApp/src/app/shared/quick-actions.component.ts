import { Component, Input, OnInit, inject } from '@angular/core';
import { combineLatest } from 'rxjs';
import { TranslocoService } from '@jsverse/transloco';
import { AuthService } from './auth.service';
import { extractValidationErrors } from './form-utils';
import {
  HomeActionMeta, MAX_HOME_ACTIONS, availableHomeActions, resolveHomeActions
} from './home-actions';
import { UpdateMyHomeActionsCommand, UsersClient } from '../web-api-client';

/**
 * The quick-action strip on a landing screen, and the picker that decides what is
 * in it. Shared by the platform-admin console dashboard and the agency home, so
 * both screens offer the same choose-your-own-shortcuts behaviour from one place;
 * `scope` is what makes the console offer console actions and the home offer the
 * agency's.
 *
 * The selection is one list per account (see resolveHomeActions): a platform
 * administrator who customizes the console and then customizes an agency
 * workspace's home replaces the console selection — each screen falls back to its
 * own defaults rather than showing the other's actions.
 */
@Component({
  selector: 'app-quick-actions',
  templateUrl: './quick-actions.component.html',
  styleUrls: ['./quick-actions.component.css']
})
export class QuickActionsComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);

  @Input() scope: 'platform' | 'agency' = 'agency';

  // Everything this user could keep, and what they keep, in their order.
  available: HomeActionMeta[] = [];
  actions: HomeActionMeta[] = [];

  // Open while the user is choosing. `draft` is the selection being edited — the
  // strip only changes once it is saved.
  customizing = false;
  draft: string[] = [];
  saving = false;
  saveError = '';

  readonly maxActions = MAX_HOME_ACTIONS;

  constructor(private auth: AuthService, private usersClient: UsersClient) { }

  ngOnInit() {
    // Both together: what may be offered comes from the user, what is offered
    // from their stored choice, and the second is resolved against the first.
    combineLatest([this.auth.currentUser$, this.auth.homeActions$])
      .subscribe(([user, stored]) => {
        this.available = availableHomeActions(user, this.scope);
        this.actions = resolveHomeActions(stored, this.available, this.scope);
      });
  }

  startCustomizing() {
    this.draft = this.actions.map(a => a.key);
    this.saveError = '';
    this.customizing = true;
  }

  cancelCustomizing() {
    this.customizing = false;
    this.saveError = '';
  }

  isPicked(key: string): boolean {
    return this.draft.includes(key);
  }

  // The picked list is what the panel shows in order; everything else is offered
  // below it. Newly checked actions join the end, where the user can see them.
  togglePick(key: string) {
    if (this.isPicked(key)) {
      this.draft = this.draft.filter(k => k !== key);
      return;
    }

    if (this.draft.length >= MAX_HOME_ACTIONS) return;

    this.draft = [...this.draft, key];
  }

  get draftActions(): HomeActionMeta[] {
    return this.draft
      .map(key => this.available.find(a => a.key === key))
      .filter((a): a is HomeActionMeta => a !== undefined);
  }

  get unpickedActions(): HomeActionMeta[] {
    return this.available.filter(a => !this.isPicked(a.key));
  }

  get isFull(): boolean {
    return this.draft.length >= MAX_HOME_ACTIONS;
  }

  // Up/down rather than drag: it works from the keyboard, and it mirrors itself
  // in Arabic without any right-to-left handling.
  moveUp(index: number) {
    if (index <= 0) return;
    const next = [...this.draft];
    [next[index - 1], next[index]] = [next[index], next[index - 1]];
    this.draft = next;
  }

  moveDown(index: number) {
    if (index >= this.draft.length - 1) return;
    const next = [...this.draft];
    [next[index], next[index + 1]] = [next[index + 1], next[index]];
    this.draft = next;
  }

  save() {
    this.saving = true;
    this.saveError = '';

    const actions = [...this.draft];

    this.usersClient.updateMyHomeActions(new UpdateMyHomeActionsCommand({ actions })).subscribe({
      next: () => {
        this.saving = false;
        this.customizing = false;
        this.actions = resolveHomeActions(actions, this.available, this.scope);
        // The current-user probe is fetched once per page load, so the service
        // has to be told — otherwise coming back to this screen would show the
        // selection from before this save.
        this.auth.markHomeActions(actions);
      },
      error: err => {
        this.saving = false;
        this.saveError =
          extractValidationErrors(err) ?? this.transloco.translate('home.actionsSaveFailed');
      }
    });
  }
}
