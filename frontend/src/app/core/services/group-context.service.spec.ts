import { afterEach, describe, expect, it } from 'vitest';
import { GroupRole, GroupUserStatus, TournamentType } from '../models/enums';
import { MyGroup } from '../models/models';
import { GroupContextService, storedGroupId } from './group-context.service';

const group: MyGroup = {
  groupId: 'g1',
  groupName: 'Palpitão England',
  slug: 'eng',
  role: GroupRole.GroupAdmin,
  status: GroupUserStatus.Approved,
  tournamentType: TournamentType.PalpitaoEngland,
  allowParticipantsToViewOthersPredictions: false,
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

  it('tracks the tournament type (World Cup vs England)', () => {
    const svc = new GroupContextService();
    expect(svc.isWorldCup()).toBe(false);

    svc.select({ ...group, tournamentType: TournamentType.FifaWorldCup });
    expect(svc.tournamentType()).toBe(TournamentType.FifaWorldCup);
    expect(svc.isWorldCup()).toBe(true);
  });

  it('lets a participant view others only when the group enables it', () => {
    const svc = new GroupContextService();
    // Participant, feature disabled -> cannot view.
    svc.select({ ...group, role: GroupRole.Participant });
    expect(svc.allowViewOthersPredictions()).toBe(false);
    expect(svc.canViewOthersPredictions()).toBe(false);

    // Participant, feature enabled -> can view.
    svc.select({
      ...group,
      role: GroupRole.Participant,
      allowParticipantsToViewOthersPredictions: true,
    });
    expect(svc.canViewOthersPredictions()).toBe(true);
  });

  it('always lets a group admin view others, regardless of the setting', () => {
    const svc = new GroupContextService();
    svc.select({ ...group, role: GroupRole.GroupAdmin });
    expect(svc.allowViewOthersPredictions()).toBe(false);
    expect(svc.canViewOthersPredictions()).toBe(true);
  });

  it('updates the cached flag when the admin toggles the setting', () => {
    const svc = new GroupContextService();
    svc.select({ ...group, role: GroupRole.Participant });
    expect(svc.canViewOthersPredictions()).toBe(false);

    svc.setAllowViewOthersPredictions(true);
    expect(svc.allowViewOthersPredictions()).toBe(true);
    expect(svc.canViewOthersPredictions()).toBe(true);
  });
});
