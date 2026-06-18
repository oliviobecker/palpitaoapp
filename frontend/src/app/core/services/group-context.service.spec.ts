import { afterEach, describe, expect, it } from 'vitest';
import { GroupRole, GroupUserStatus } from '../models/enums';
import { MyGroup } from '../models/models';
import { GroupContextService, storedGroupId } from './group-context.service';

const group: MyGroup = {
  groupId: 'g1',
  groupName: 'Palpitão England',
  slug: 'eng',
  role: GroupRole.GroupAdmin,
  status: GroupUserStatus.Approved,
  isActive: true,
};

describe('GroupContextService', () => {
  afterEach(() => localStorage.clear());

  it('selects a group, exposing it through signals and storage', () => {
    const svc = new GroupContextService();
    svc.select(group);

    expect(svc.groupId()).toBe('g1');
    expect(svc.groupName()).toBe('Palpitão England');
    expect(svc.hasGroup()).toBe(true);
    expect(svc.isGroupAdmin()).toBe(true);
    expect(storedGroupId()).toBe('g1');
  });

  it('clears the current group', () => {
    const svc = new GroupContextService();
    svc.select(group);
    svc.clear();

    expect(svc.groupId()).toBeNull();
    expect(svc.hasGroup()).toBe(false);
    expect(svc.isGroupAdmin()).toBe(false);
    expect(storedGroupId()).toBeNull();
  });

  it('is not a group admin for participant memberships', () => {
    const svc = new GroupContextService();
    svc.select({ ...group, role: GroupRole.Participant });

    expect(svc.hasGroup()).toBe(true);
    expect(svc.isGroupAdmin()).toBe(false);
  });
});
