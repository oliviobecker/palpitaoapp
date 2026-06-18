import { Routes } from '@angular/router';
import { adminGuard, authGuard, participantGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/auth/login').then((m) => m.Login) },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register').then((m) => m.Register),
  },
  {
    path: 'create-group',
    loadComponent: () => import('./features/auth/create-group').then((m) => m.CreateGroup),
  },
  {
    path: 'select-group',
    canActivate: [authGuard],
    loadComponent: () => import('./features/groups/select-group').then((m) => m.SelectGroup),
  },
  {
    path: 'pending',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/groups/awaiting-approval').then((m) => m.AwaitingApproval),
  },
  {
    path: '',
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        canActivate: [participantGuard],
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'rounds',
        canActivate: [participantGuard],
        loadComponent: () => import('./features/rounds/rounds').then((m) => m.Rounds),
      },
      {
        path: 'rounds/:id/predictions',
        loadComponent: () => import('./features/rounds/predictions').then((m) => m.Predictions),
      },
      {
        path: 'rounds/:id/mirror',
        loadComponent: () => import('./features/rounds/mirror').then((m) => m.Mirror),
      },
      {
        path: 'rounds/:id/results',
        loadComponent: () => import('./features/rounds/results').then((m) => m.Results),
      },
      {
        path: 'rounds/:id/temporary-standings',
        loadComponent: () =>
          import('./features/rounds/temporary-standings').then((m) => m.TemporaryStandingsView),
      },
      {
        path: 'standings',
        canActivate: [participantGuard],
        loadComponent: () => import('./features/standings/standings').then((m) => m.Standings),
      },
      {
        path: 'admin',
        canActivate: [adminGuard],
        children: [
          { path: '', loadComponent: () => import('./features/admin/admin').then((m) => m.Admin) },
          {
            path: 'seasons',
            loadComponent: () =>
              import('./features/admin/admin-seasons').then((m) => m.AdminSeasons),
          },
          {
            path: 'rounds',
            loadComponent: () => import('./features/admin/admin-rounds').then((m) => m.AdminRounds),
          },
          {
            path: 'rounds/new',
            loadComponent: () =>
              import('./features/admin/admin-round-form').then((m) => m.AdminRoundForm),
          },
          {
            path: 'rounds/:id',
            loadComponent: () =>
              import('./features/admin/admin-round-detail').then((m) => m.AdminRoundDetail),
          },
          {
            path: 'rounds/:id/matches',
            loadComponent: () =>
              import('./features/admin/admin-matches').then((m) => m.AdminMatches),
          },
          {
            path: 'rounds/:id/scout',
            loadComponent: () =>
              import('./features/admin/admin-round-scout').then((m) => m.AdminRoundScout),
          },
          {
            path: 'rounds/:id/manual-predictions',
            loadComponent: () =>
              import('./features/admin/admin-manual-predictions').then(
                (m) => m.AdminManualPredictions,
              ),
          },
          {
            path: 'rounds/:id/import-predictions',
            loadComponent: () =>
              import('./features/admin/admin-ocr-import').then((m) => m.AdminOcrImport),
          },
          {
            path: 'participants',
            loadComponent: () =>
              import('./features/admin/admin-participants').then((m) => m.AdminParticipants),
          },
          {
            path: 'registration-requests',
            loadComponent: () =>
              import('./features/admin/admin-registration-requests').then(
                (m) => m.AdminRegistrationRequests,
              ),
          },
          {
            path: 'audit',
            loadComponent: () => import('./features/admin/admin-audit').then((m) => m.AdminAudit),
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
