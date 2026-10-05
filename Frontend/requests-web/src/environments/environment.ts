/**
 * Production environment configuration.
 * The API base URL is sourced here so that no API URL is hardcoded elsewhere (R15.6).
 *
 * NOTE: this is an exercise project with no deployed backend, so `apiBaseUrl` intentionally points
 * at the same local API as development. In a real deployment this would be the public API origin
 * (e.g. `https://api.example.com`), typically injected at build/deploy time.
 */
export const environment = {
  production: true,
  apiBaseUrl: 'http://localhost:60702',
};
