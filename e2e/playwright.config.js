// Runs against an already running sample site:
//   EPT_BASE_URL    site root (default https://localhost:5000)
//   EPT_USER        CMS administrator (default Admin)
//   EPT_PASSWORD    its password (required, never committed)
//   EPT_SHELL_PATH  CMS shell mount point (default /EPiServer, the CMS default)
const { defineConfig } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './tests',
  timeout: 180_000,
  expect: { timeout: 30_000 },
  workers: 1,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.EPT_BASE_URL || 'https://localhost:5000',
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'setup', testMatch: /auth\.setup\.js/ },
    {
      name: 'chromium',
      use: { browserName: 'chromium', storageState: '.auth/admin.json' },
      dependencies: ['setup'],
    },
  ],
});
