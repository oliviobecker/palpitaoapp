import { Routes } from '@angular/router';
import { adminGuard, authGuard, participantGuard } from '@core/auth/auth.guard';
import { unsavedChangesGuard } from '@core/guards/unsaved-changes.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./features/landing/landing').then((m) => m.Landing),
  },
  { path: 'login', loadComponent: () => import('./features/auth/login').then((m) => m.Login) },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register').then((m) => m.Register),
  },
  {
    path: 'create-group',
    loadComponent: () => import('./features/auth/create-group').then((m) => m.CreateGroup),
  },
  // Public standings link: no guard, no Shell, no session. The key comes from the path
  // (/p/:key) or the query string (/p?key=...), so a pasted link works either way.
  {
    path: 'p',
    loadComponent: () =>
      import('./features/public/public-standings').then((m) => m.PublicStandings),
  },
  {
    path: 'p/:key',
    loadComponent: () =>
      import('./features/public/public-standings').then((m) => m.PublicStandings),
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
        canActivate: [participantGuard],
        canDeactivate: [unsavedChangesGuard],
        loadComponent: () => import('./features/rounds/predictions').then((m) => m.Predictions),
      },
      {
        path: 'rounds/:id/mirror',
        canActivate: [participantGuard],
        loadComponent: () => import('./features/rounds/mirror').then((m) => m.Mirror),
      },
      {
        path: 'rounds/:id/results',
        canActivate: [participantGuard],
        loadComponent: () => import('./features/rounds/results').then((m) => m.Results),
      },
      {
        path: 'rounds/:id/temporary-standings',
        canActivate: [participantGuard],
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
        loadComponent: () =>
          import('./features/admin/shell/admin-layout').then((m) => m.AdminLayout),
        loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
