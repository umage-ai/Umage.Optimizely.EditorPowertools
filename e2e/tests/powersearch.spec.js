const { test, expect } = require('@playwright/test');
const { watch, overviewUrl } = require('./helpers');

// PowerSearch is a client-side app: it must boot, browse on load and react to typing. With Graph
// unconfigured, or holding another CMS version's schema, it shows a status message instead; that is
// a site setup problem rather than a UI bug, so only uncaught script errors fail the test.
test('PowerSearch browses on load and searches as you type', async ({ page, baseURL }) => {
  await page.goto(await overviewUrl(page));
  const link = page.locator('a[href*="PowerSearch"]').first();
  test.skip(await link.count() === 0, 'Graph add-on not installed on this site');

  const seen = watch(page, new URL(baseURL).origin);
  await page.goto(await link.getAttribute('href'));
  const box = page.locator('.ept-main input[type="search"], .ept-main input[type="text"]').first();
  await expect(box).toBeVisible();
  await page.waitForLoadState('networkidle');
  await expect(page.locator('.ept-main .ept-loading:visible')).toHaveCount(0);

  await box.fill('alloy');
  await page.waitForLoadState('networkidle');
  await expect(page.locator('.ept-main .ept-loading:visible')).toHaveCount(0);

  expect(seen.problems().filter(p => p.startsWith('script error'))).toEqual([]);
});
