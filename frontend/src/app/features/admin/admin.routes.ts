import { Routes } from '@angular/router';
import { unsavedChangesGuard } from '@core/guards/unsaved-changes.guard';

/** The admin area's pages, under the `admin` route (its shell and guard are set in app.routes.ts). */
export const ADMIN_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./dashboard/admin').then((m) => m.Admin) },
  {
    path: 'seasons',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./seasons/admin-seasons').then((m) => m.AdminSeasons),
  },
  {
    path: 'rounds',
    loadComponent: () => import('./rounds/admin-rounds').then((m) => m.AdminRounds),
  },
  {
    path: 'rounds/new',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./rounds/admin-round-form').then((m) => m.AdminRoundForm),
  },
  {
    path: 'rounds/:id',
    loadComponent: () => import('./rounds/admin-round-detail').then((m) => m.AdminRoundDetail),
  },
  {
    path: 'rounds/:id/matches',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./rounds/admin-matches').then((m) => m.AdminMatches),
  },
  {
    path: 'rounds/:id/audit',
    loadComponent: () => import('./rounds/admin-round-audit').then((m) => m.AdminRoundAudit),
  },
  {
    path: 'rounds/:id/scout',
    loadComponent: () => import('./rounds/admin-round-scout').then((m) => m.AdminRoundScout),
  },
  {
    path: 'rounds/:id/manual-predictions',
    loadComponent: () =>
      import('./predictions/admin-manual-predictions').then((m) => m.AdminManualPredictions),
  },
  {
    path: 'rounds/:id/import-predictions',
    loadComponent: () => import('./ocr/admin-ocr-import').then((m) => m.AdminOcrImport),
  },
  {
    path: 'rounds/:id/import-history',
    loadComponent: () => import('./ocr/admin-ocr-history').then((m) => m.AdminOcrHistory),
  },
  {
    path: 'ocr-aliases',
    loadComponent: () => import('./ocr/admin-ocr-aliases').then((m) => m.AdminOcrAliases),
  },
  {
    path: 'scoring',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./scoring/admin-scoring-rules').then((m) => m.AdminScoringRules),
  },
  {
    path: 'participants',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () =>
      import('./participants/admin-participants').then((m) => m.AdminParticipants),
  },
  {
    path: 'registration-requests',
    loadComponent: () =>
      import('./participants/admin-registration-requests').then((m) => m.AdminRegistrationRequests),
  },
  {
    path: 'teams',
    loadComponent: () => import('./teams/admin-teams').then((m) => m.AdminTeams),
  },
  {
    path: 'audit',
    loadComponent: () => import('./audit/admin-audit').then((m) => m.AdminAudit),
  },
];
