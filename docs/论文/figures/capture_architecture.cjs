const { chromium } = require('playwright');
const { pathToFileURL } = require('node:url');
const path = require('node:path');

const targets = [
  {
    html: 'datawatch_system_architecture.html',
    png: 'datawatch_system_architecture.png',
    viewport: { width: 1800, height: 1000 },
  },
  {
    html: 'datawatch_block_coalescing.html',
    png: 'datawatch_block_coalescing.png',
    viewport: { width: 1100, height: 900 },
  },
];

async function main() {
  const browser = await chromium.launch({
    headless: true,
    executablePath: 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  });
  try {
    for (const target of targets) {
      const page = await browser.newPage({
        viewport: target.viewport,
        deviceScaleFactor: 2,
        colorScheme: 'light',
      });
      const htmlPath = path.resolve(__dirname, target.html);
      const pngPath = path.resolve(__dirname, target.png);
      await page.goto(pathToFileURL(htmlPath).href, { waitUntil: 'networkidle' });
      await page.locator('svg').first().screenshot({ path: pngPath });
      await page.close();
    }
  } finally {
    await browser.close();
  }
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
