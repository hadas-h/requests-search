import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
  inject,
  provideAppInitializer,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import {
  HTTP_INTERCEPTORS,
  provideHttpClient,
  withInterceptorsFromDi,
} from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeuix/themes/aura';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';

import { routes } from './app.routes';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';

/**
 * Default UI language. Kept as a constant so it is not hardcoded across the app.
 */
export const DEFAULT_LANGUAGE = 'en';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptorsFromDi()),
    {
      provide: HTTP_INTERCEPTORS,
      useClass: AuthInterceptor,
      multi: true,
    },
    provideAnimationsAsync(),
    providePrimeNG({
      theme: {
        preset: Aura,
        options: {
          // Disable dark-mode auto-switching for a predictable back-office look.
          darkModeSelector: false,
        },
      },
    }),
    provideTranslateService({
      fallbackLang: DEFAULT_LANGUAGE,
    }),
    provideTranslateHttpLoader({
      prefix: 'assets/i18n/',
      suffix: '.json',
    }),
    provideAppInitializer(() => {
      const translate = inject(TranslateService);
      translate.setFallbackLang(DEFAULT_LANGUAGE);
      return translate.use(DEFAULT_LANGUAGE);
    }),
  ],
};
