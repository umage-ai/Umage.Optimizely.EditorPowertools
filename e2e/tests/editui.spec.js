const { test, expect } = require('@playwright/test');
const { shellPath, watch } = require('./helpers');

// The CMS edit UI must load with the Powertools add-ons and their Dojo modules registered, and the
// rich-text editor must start: TinyMCE is the first thing to break on a runtime mismatch.
test('edit UI loads and the rich-text editor starts', async ({ page, baseURL }) => {
  // A page with a main body (the sample sites' StandardPage), found through Content Type Audit,
  // which also hands back its edit URL.
  const api = `${shellPath}/EditorPowertools/ContentTypeAuditApi`;
  const headers = { 'X-Requested-With': 'XMLHttpRequest' };
  const types = await (await page.request.get(`${api}/GetTypes`, { headers })).json();
  const standard = types.find(t => t.name === 'StandardPage');
  expect(standard, 'StandardPage content type').toBeTruthy();
  const pages = await (await page.request.get(`${api}/GetContentOfType/${standard.id}`, { headers })).json();
  expect(pages.length, 'StandardPage instances').toBeGreaterThan(0);

  const seen = watch(page, new URL(baseURL).origin);
  await page.goto(pages[0].editUrl);
  await page.waitForLoadState('networkidle');

  // CMS 12 opens on-page editing (switch to the form); CMS 13 shows the form beside the preview.
  const allProperties = page.getByRole('button', { name: 'All Properties' });
  if (await allProperties.count() > 0 && await allProperties.isEnabled()) await allProperties.click();
  // TinyMCE's editing iframe (id ends in "_ifr" in every version) with an editable body means it started.
  const editor = page.frameLocator('iframe[id$="_ifr"]').first();
  await expect(editor.locator('body[contenteditable="true"]')).toBeAttached({ timeout: 60_000 });

  expect(seen.problems()).toEqual([]);
});
