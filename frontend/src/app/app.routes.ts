import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent),
    canActivate: [authGuard]
  },
  {
    path: 'searchlists',
    loadComponent: () => import('./features/searchlists/searchlists.component').then(m => m.SearchListsComponent),
    canActivate: [authGuard]
  },
  {
    path: 'jobs',
    loadComponent: () => import('./features/jobs/jobs.component').then(m => m.JobsComponent),
    canActivate: [authGuard]
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
