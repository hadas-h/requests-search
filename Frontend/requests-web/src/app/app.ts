import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';

import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, TranslatePipe, ButtonModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly user = this.auth.user;
  protected readonly isAuthenticated = this.auth.isAuthenticated;
  protected readonly isAdministrator = computed(() => this.user()?.isAdministrator ?? false);

  // A regular signed-in user sees "My Requests"; admins (and the login screen) keep "Requests".
  protected readonly titleKey = computed(() =>
    this.isAuthenticated() && !this.isAdministrator() ? 'App.titleMyRequests' : 'App.title',
  );

  protected logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
