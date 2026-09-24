// 资源下载脚本 — https://tikhub.io/zh
// 下载 logo + 3 个数据集图到 public/sites/tikhub-io-5f62aaaa/

import { mkdir, writeFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(__dirname, "..");

const ASSETS = [
  {
    name: "logo",
    url: "https://tikhub.io/logo.jpeg",
    dest: "public/sites/tikhub-io-5f62aaaa/shared/logo.jpeg",
  },
  {
    name: "tiktok-video-dataset-export",
    url: "https://tikhub.io/images/datasets/tiktok-video-dataset-export.webp",
    dest: "public/sites/tikhub-io-5f62aaaa/zh-d83abd2e/images/tiktok-video-dataset-export.webp",
  },
  {
    name: "social-profile-dataset-fields",
    url: "https://tikhub.io/images/datasets/social-profile-dataset-fields.webp",
    dest: "public/sites/tikhub-io-5f62aaaa/zh-d83abd2e/images/social-profile-dataset-fields.webp",
  },
  {
    name: "social-engagement-dataset",
    url: "https://tikhub.io/images/datasets/social-engagement-dataset.webp",
    dest: "public/sites/tikhub-io-5f62aaaa/zh-d83abd2e/images/social-engagement-dataset.webp",
  },
];

async function download(url, dest) {
  const fullPath = join(ROOT, dest);
  await mkdir(dirname(fullPath), { recursive: true });
  const res = await fetch(url, { redirect: "follow" });
  if (!res.ok) {
    throw new Error(`HTTP ${res.status} for ${url}`);
  }
  const buffer = Buffer.from(await res.arrayBuffer());
  await writeFile(fullPath, buffer);
  return buffer.length;
}

let ok = 0;
let fail = 0;
for (const a of ASSETS) {
  try {
    const bytes = await download(a.url, a.dest);
    console.log(`✓ ${a.name} (${(bytes / 1024).toFixed(1)} KB) -> ${a.dest}`);
    ok++;
  } catch (e) {
    console.error(`✗ ${a.name}: ${e.message}`);
    fail++;
  }
}

console.log(`\n完成: ${ok} 个成功, ${fail} 个失败 (共 ${ASSETS.length} 个)`);
process.exit(fail > 0 ? 1 : 0);
