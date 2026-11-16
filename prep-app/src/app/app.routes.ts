import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: 'login', loadComponent: () => import('./login/login').then((m) => m.Login) },
  { path: 'signup', loadComponent: () => import('./signup/signup').then((m) => m.Signup) },
  { path: 'home', loadComponent: () => import('./home/home').then((m) => m.Home) },
  { path: '**', redirectTo: 'login' },
];
