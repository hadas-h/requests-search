import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';

import { AuthService } from '../../core/auth/auth.service';

/** Login / register screen. Toggles between the two modes and authenticates via AuthService. */
@Component({
  selector: 'app-auth-page',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe, InputTextModule, PasswordModule, ButtonModule],
  templateUrl: './auth-page.component.html',
  styleUrl: './auth-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly isRegister = signal(false);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.group({
    username: ['', Validators.required],
    password: ['', Validators.required],
  });

  protected toggleMode(): void {
    this.isRegister.update((v) => !v);
    this.errorMessage.set(null);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { username, password } = this.form.getRawValue();
    this.submitting.set(true);
    this.errorMessage.set(null);

    const request$ = this.isRegister()
      ? this.auth.register(username!, password!)
      : this.auth.login(username!, password!);

    request$.subscribe({
      next: () => this.router.navigate(['']),
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(err?.error?.message ?? null);
      },
    });
  }
}
