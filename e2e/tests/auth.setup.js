const { test: setup } = require('@playwright/test');
const { shellPath } = require('./helpers');

setup('sign in as an administrator', async ({ page }) => {
  const password = process.env.EPT_PASSWORD;
  if (!password) {
    throw new Error('Set EPT_PASSWORD (and EPT_USER if not "Admin") to a CMS administrator on the site under test.');
  }

  await page.goto(`/util/Login?ReturnUrl=${encodeURIComponent(`${shellPath}/CMS/`)}`);
  await page.getByLabel('Name').fill(process.env.EPT_USER || 'Admin');
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Log in' }).click();
  await page.waitForURL(url => !url.pathname.toLowerCase().includes('/util/login'));
  await page.context().storageState({ path: '.auth/admin.json' });
});
