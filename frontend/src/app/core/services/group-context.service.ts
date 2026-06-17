import { Injectable, computed, signal } from '@angular/core';
import { GroupRole } from '../models/enums';
import { MyGroup } from '../models/models';

const GROUP_ID_KEY = 'palpitao.groupId';
const GROUP_NAME_KEY = 'palpitao.groupName';
const GROUP_ROLE_KEY = 'palpitao.groupRole';

/** Reads the current group id straight from storage (no DI) for the interceptor. */
export function storedGroupId(): string | null {
  return localStorage.getItem(GROUP_ID_KEY);
}

/**
 * Holds the "current group" the user is acting within. Persisted in localStorage
 * so a page reload keeps the context. The role drives the route guards. The certame
 * type lives on the season (not the group), so it is resolved per round, not here.
 */
@Injectable({ providedIn: 'root' })
export class GroupContextService {
  private readonly _groupId = signal<string | null>(localStorage.getItem(GROUP_ID_KEY));
  private readonly _groupName = signal<string | null>(localStorage.getItem(GROUP_NAME_KEY));
  private readonly _role = signal<GroupRole | null>(
    localStorage.getItem(GROUP_ROLE_KEY) as GroupRole | null,
  );

  readonly groupId = this._groupId.asReadonly();
  readonly groupName = this._groupName.asReadonly();
  readonly role = this._role.asReadonly();
  readonly hasGroup = computed(() => this._groupId() !== null);
  readonly isGroupAdmin = computed(() => this._role() === GroupRole.GroupAdmin);

  /** Selects a group as the current acting context. */
  select(group: MyGroup): void {
    localStorage.setItem(GROUP_ID_KEY, group.groupId);
    localStorage.setItem(GROUP_NAME_KEY, group.groupName);
    localStorage.setItem(GROUP_ROLE_KEY, group.role);
    this._groupId.set(group.groupId);
    this._groupName.set(group.groupName);
    this._role.set(group.role);
  }

  /** Clears the current group (logout / switch group). */
  clear(): void {
    localStorage.removeItem(GROUP_ID_KEY);
    localStorage.removeItem(GROUP_NAME_KEY);
    localStorage.removeItem(GROUP_ROLE_KEY);
    this._groupId.set(null);
    this._groupName.set(null);
    this._role.set(null);
  }
}
