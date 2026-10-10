// Pending sign-ups awaiting a group admin.
import { GroupUserStatus } from './enums';

export interface RegistrationRequest {
  /** GroupUser id of the membership request. */
  id: string;
  userId: string;
  name: string;
  email: string;
  createdAt: string;
  status: GroupUserStatus;
}
