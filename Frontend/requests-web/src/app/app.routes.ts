import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';
import { AuthPageComponent } from './features/auth/auth-page.component';
import { RequestsHomeComponent } from './features/requests/requests-home/requests-home.component';

/**
 * Application routes. The requests home (search + dashboard tabs) is the default route, protected
 * by the auth guard; unauthenticated users are redirected to the login screen.
 */
export const routes: Routes = [
  { path: 'login', component: AuthPageComponent },
  { path: '', component: RequestsHomeComponent, canActivate: [authGuard] },
];
