// The admin audit trail.

export interface AuditLog {
  id: string;
  userId?: string | null;
  userName?: string | null;
  action: string;
  entityName: string;
  entityId?: string | null;
  details?: string | null;
  createdAt: string;
}

export interface AuditFilter {
  userId?: string;
  entityName?: string;
  from?: string;
  to?: string;
}
