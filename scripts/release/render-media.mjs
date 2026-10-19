// Renders media/src/brand.html into the PNGs under media/ (icon, GitHub social preview, BOOTH images).
//   PLAYWRIGHT=<path to playwright-core> node scripts/release/render-media.mjs [browser executable]
// Uses an installed Chromium-based browser (Edge or Chrome); nothing is downloaded.
import { createRequire } from 'node:module';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT || 'playwright-core');
const root = resolve(import.meta.dirname, '../..');
const page = pathToFileURL(resolve(root, 'media/src/brand.html')).href;
const executablePath = process.argv[2] || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';

const outputs = [
  ['icon', 'media/icon-512.png', 1],
  ['icon', 'media/icon-160.png', 160 / 512],
  ['social', 'media/social-preview.png', 1],
  ['booth-01', 'media/booth/booth-01-main.png', 1],
  ['booth-02', 'media/booth/booth-02-reasons.png', 1],
  ['booth-03', 'media/booth/booth-03-tools.png', 1],
  ['booth-04', 'media/booth/booth-04-setup.png', 1],
  ['booth-05', 'media/booth/booth-05-sample.png', 1],
  ['booth-06', 'media/booth/booth-06-comparison.png', 1],
];

await mkdir(resolve(root, 'media/booth'), { recursive: true });
const browser = await chromium.launch({ executablePath });
for (const [frame, out, scale] of outputs) {
  const context = await browser.newContext({ viewport: { width: 1600, height: 1600 }, deviceScaleFactor: scale });
  const tab = await context.newPage();
  await tab.goto(`${page}?frame=${frame}`);
  await tab.evaluate(() => document.fonts.ready);
  await tab.locator(`#${frame}`).screenshot({ path: resolve(root, out) });
  console.log(out);
  await context.close();
}
await browser.close();
