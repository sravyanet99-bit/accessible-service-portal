import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  timeout: 30_000,
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: 'http://localhost:5080',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
  ],
  webServer: {
    command: 'dotnet run --project ../../src/ServicePortal.Api --no-launch-profile --urls http://localhost:5080',
    url: 'http://localhost:5080/health/ready',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    env: { ASPNETCORE_ENVIRONMENT: 'Testing' },
  },
});
