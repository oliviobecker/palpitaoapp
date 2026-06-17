import { Injectable, computed, signal } from '@angular/core';
import { GroupRole, TournamentType } from '../models/enums';
import { MyGroup } from '../models/models';

const GROUP_ID_KEY = 'palpitao.groupId';
const GROUP_NAME_KEY = 'palpitao.groupName';
const GROUP_ROLE_KEY = 'palpitao.groupRole';
const GROUP_TYPE_KEY = 'palpitao.groupType';
const GROUP_VIEW_OTHERS_KEY = 'palpitao.groupViewOthers';
const GROUP_ALLOW_SUBMIT_KEY = 'palpitao.groupAllowSubmit';

/** Reads the current group id straight from storage (no DI) for the interceptor. */
export function storedGroupId(): string | null {
  return localStorage.getItem(GROUP_ID_KEY);
}

/**
 * Holds the "current group" the user is acting within. Persisted in localStorage
 * so a page reload keeps the context. The role drives the route guards.
 */
@Injectable({ providedIn: 'root' })
export class GroupContextService {
  private readonly _groupId = signal<string | null>(localStorage.getItem(GROUP_ID_KEY));
  private readonly _groupName = signal<string | null>(localStorage.getItem(GROUP_NAME_KEY));
  private readonly _role = signal<GroupRole | null>(
    localStorage.getItem(GROUP_ROLE_KEY) as GroupRole | null,
  );
  private readonly _tournamentType = signal<TournamentType | null>(
    localStorage.getItem(GROUP_TYPE_KEY) as TournamentType | null,
  );
  private readonly _allowViewOthers = signal<boolean>(
    localStorage.getItem(GROUP_VIEW_OTHERS_KEY) === 'true',
  );
  // Defaults to true (in-app submission) when unset, matching the backend default.
  private readonly _allowSubmit = signal<boolean>(
    localStorage.getItem(GROUP_ALLOW_SUBMIT_KEY) !== 'false',
  );

  readonly groupId = this._groupId.asReadonly();
  readonly groupName = this._groupName.asReadonly();
  readonly role = this._role.asReadonly();
  readonly tournamentType = this._tournamentType.asReadonly();
  readonly hasGroup = computed(() => this._groupId() !== null);
  readonly isGroupAdmin = computed(() => this._role() === GroupRole.GroupAdmin);
  readonly isWorldCup = computed(() => this._tournamentType() === TournamentType.FifaWorldCup);
  /** Whether the current group lets participants view others' predictions. */
  readonly allowViewOthersPredictions = this._allowViewOthers.asReadonly();
  /** Who may open the prediction mirror: group admins always, participants when enabled. */
  readonly canViewOthersPredictions = computed(
    () => this.isGroupAdmin() || this._allowViewOthers(),
  );
  /** Whether participants submit predictions in the app (false = admin-only mode). */
  readonly allowParticipantsToSubmit = this._allowSubmit.asReadonly();

  /** Selects a group as the current acting context. */
  select(group: MyGroup): void {
    localStorage.setItem(GROUP_ID_KEY, group.groupId);
    localStorage.setItem(GROUP_NAME_KEY, group.groupName);
    localStorage.setItem(GROUP_ROLE_KEY, group.role);
    localStorage.setItem(GROUP_TYPE_KEY, group.tournamentType);
    localStorage.setItem(
      GROUP_VIEW_OTHERS_KEY,
      String(group.allowParticipantsToViewOthersPredictions),
    );
    localStorage.setItem(
      GROUP_ALLOW_SUBMIT_KEY,
      String(group.allowParticipantsToSubmitPredictions),
    );
    this._groupId.set(group.groupId);
    this._groupName.set(group.groupName);
    this._role.set(group.role);
    this._tournamentType.set(group.tournamentType);
    this._allowViewOthers.set(group.allowParticipantsToViewOthersPredictions);
    this._allowSubmit.set(group.allowParticipantsToSubmitPredictions);
  }

  /** Updates the cached "view others" flag (after an admin toggles the setting). */
  setAllowViewOthersPredictions(value: boolean): void {
    localStorage.setItem(GROUP_VIEW_OTHERS_KEY, String(value));
    this._allowViewOthers.set(value);
  }

  /** Updates the cached "in-app submission" flag (after an admin toggles the setting). */
  setAllowParticipantsToSubmit(value: boolean): void {
    localStorage.setItem(GROUP_ALLOW_SUBMIT_KEY, String(value));
    this._allowSubmit.set(value);
  }

  /** Clears the current group (logout / switch group). */
  clear(): void {
    localStorage.removeItem(GROUP_ID_KEY);
    localStorage.removeItem(GROUP_NAME_KEY);
    localStorage.removeItem(GROUP_ROLE_KEY);
    localStorage.removeItem(GROUP_TYPE_KEY);
    localStorage.removeItem(GROUP_VIEW_OTHERS_KEY);
    localStorage.removeItem(GROUP_ALLOW_SUBMIT_KEY);
    this._groupId.set(null);
    this._groupName.set(null);
    this._role.set(null);
    this._tournamentType.set(null);
    this._allowViewOthers.set(false);
    this._allowSubmit.set(true);
  }
}
