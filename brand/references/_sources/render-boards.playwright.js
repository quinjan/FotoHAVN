// Renders every .board in boards.html to its reference PNG.
// Serve the repository root over HTTP, open /brand/references/_sources/boards.html in a
// Playwright page at a viewport at least 1680px wide, then run this function with that page.
async (page) => {
  await page.evaluate(async () => {
    await document.fonts.ready;
    await Promise.all([...document.images].map((i) => (i.complete ? 1 : new Promise((r) => { i.onload = i.onerror = r; }))));
  });
  const fonts = await page.evaluate(() =>
    ['400 20px "Cormorant Garamond"', 'italic 400 20px "Cormorant Garamond"', '400 20px Manrope', '600 20px Manrope'].every((f) => document.fonts.check(f)));
  if (!fonts) throw new Error('Brand fonts did not load; boards would render with fallbacks.');
  const folders = { mood: 'mood', dodont: 'do-dont', logo: 'logo' };
  const ids = await page.$$eval('section.board', (els) => els.map((e) => e.id));
  const written = [];
  for (const id of ids) {
    const [, prefix, name] = id.match(/^(mood|dodont|logo)-(.+)$/);
    // Relative to the repository root, which must be the Playwright process working directory.
    const path = `brand/references/${folders[prefix]}/${name}.png`;
    await page.locator('#' + id).screenshot({ path });
    written.push(path);
  }
  return written;
}
