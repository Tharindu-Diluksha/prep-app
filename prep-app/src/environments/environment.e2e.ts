// End-to-end test configuration (build configuration "e2e"). Not a secret.
// Port 5246 must match E2E_API_PORT's default in playwright.config.ts.
export const environment = {
  apiBaseUrl: 'http://localhost:5246',
};
