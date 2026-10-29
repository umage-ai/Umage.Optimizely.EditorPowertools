const { test, expect } = require('@playwright/test');
const { watch, overviewUrl } = require('./helpers');

// Every tool card on the Overview page (core, Forms and Graph add-ons alike) must open, render its
// main area and settle without script errors, console errors or failed requests.
test('every tool on the Overview page opens cleanly', async ({ page, baseURL }) => {
  await page.goto(await overviewUrl(page));
  const tools = await page.locator('.ept-tool-card a[href], a.ept-tool-card[href]').evaluateAll(
    links => [...new Set(links.map(a => a.getAttribute('href')))]);
  expect(tools.length, 'tool cards on the Overview page').toBeGreaterThan(10);

  for (const href of tools) {
    await test.step(href, async () => {
      const tab = await page.context().newPage();
      const seen = watch(tab, new URL(baseURL).origin);
      await tab.goto(href);
      await tab.waitForLoadState('networkidle');
      await expect.soft(tab.locator('.ept-main')).toBeVisible();
      await expect.soft(tab.locator('.ept-main')).not.toBeEmpty();
      // Loading placeholders must be gone and no error banner shown once the network is idle.
      await expect.soft(tab.locator('.ept-main .ept-loading:visible')).toHaveCount(0);
      await expect.soft(tab.locator('.ept-main .ept-alert--danger:visible')).toHaveCount(0);
      expect.soft(seen.problems(), `problems on ${href}`).toEqual([]);
      await tab.close();
    });
  }
});
