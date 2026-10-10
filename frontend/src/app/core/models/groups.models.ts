// Groups a user belongs to or can join.
import { GroupRole, GroupUserStatus } from './enums';

/** Public, non-sensitive view of an active group (registration picker). */
export interface PublicGroup {
  id: string;
  name: string;
  slug: string;
  description?: string | null;
}

/** A group the authenticated user has approved access to. */
export interface MyGroup {
  groupId: string;
  groupName: string;
  slug: string;
  role: GroupRole;
  status: GroupUserStatus;
  /** Per-group active flag; false = deactivated by the group admin (blocked). */
  isActive: boolean;
}
