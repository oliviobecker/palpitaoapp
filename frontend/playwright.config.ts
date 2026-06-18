import { defineConfig, devices } from '@playwright/test';

/**
 * E2E config. Tests are hermetic: the Angular dev server runs, but every API call
 * to the backend host is intercepted and mocked (see e2e/support.ts), so no
 * .NET backend or database is required.
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  // The dev server lazy-compiles each route on first hit; the first navigation of
  // a cold run can take several seconds, so allow generous assertion time.
  timeout: 60_000,
  expect: { timeout: 15_000 },
  use: {
    baseURL: 'http://localhost:4200',
    locale: 'en-US',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run start -- --port 4200',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
  },
});
