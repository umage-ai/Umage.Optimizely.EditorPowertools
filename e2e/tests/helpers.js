const shellPath = process.env.EPT_SHELL_PATH || '/EPiServer';

/**
 * Collects what a page does wrong while it is open: uncaught script errors, console errors and
 * failed same-origin requests. Read `problems()` once the page has settled.
 */
function watch(page, origin) {
  const found = [];
  page.on('pageerror', e => found.push(`script error: ${e.message}`));
  page.on('console', m => { if (m.type() === 'error') found.push(`console: ${m.text()}`); });
  page.on('response', r => {
    if (r.status() >= 400 && r.url().startsWith(origin)) found.push(`HTTP ${r.status()}: ${r.url()}`);
  });
  return { problems: () => found.slice() };
}

/**
 * The Powertools Overview page. The CMS shell builds its add-on menu on demand, so the link is not in
 * the page to discover; the default protected-module path under the shell is used instead
 * (EPT_OVERVIEW_PATH overrides it). Every other tool URL is then taken from the Overview page.
 */
async function overviewUrl() {
  return process.env.EPT_OVERVIEW_PATH || `${shellPath}/EditorPowertools/EditorPowertools/Overview`;
}

module.exports = { shellPath, watch, overviewUrl };
