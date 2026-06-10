#!/usr/bin/env node

/**
 * Performance Budget Script
 * Validates asset sizes and build configuration to prevent performance regressions.
 *
 * Usage: node scripts/perf-budget.mjs
 * Exit code 0 = all checks pass, 1 = failures found.
 */

import { readdir, stat, readFile } from 'node:fs/promises';
import { join, extname } from 'node:path';
import { existsSync } from 'node:fs';

const FRONTEND_DIR = 'frontend';
const IMAGES_DIR = join(FRONTEND_DIR, 'public/images');
const ROOT_LAYOUT = join(FRONTEND_DIR, 'src/app/layout.tsx');

const SVG_MAX_KB = 50;
const HERO_MAX_KB = 500;

const results = [];

function pass(check, detail) {
  results.push({ check, status: '✅ PASS', detail });
}

function fail(check, detail) {
  results.push({ check, status: '❌ FAIL', detail });
}

async function checkSvgSizes() {
  const check = 'SVG Logo Sizes';
  try {
    const files = await readdir(IMAGES_DIR);
    const svgs = files.filter(f => extname(f) === '.svg');

    if (svgs.length === 0) {
      pass(check, 'No SVG files found');
      return;
    }

    let allGood = true;
    const details = [];
    for (const svg of svgs) {
      const filePath = join(IMAGES_DIR, svg);
      const st = await stat(filePath);
      const sizeKb = Math.round(st.size / 1024);
      if (sizeKb > SVG_MAX_KB) {
        details.push(`${svg}: ${sizeKb}KB (max ${SVG_MAX_KB}KB)`);
        allGood = false;
      } else {
        details.push(`${svg}: ${sizeKb}KB ✓`);
      }
    }

    if (allGood) {
      pass(check, details.join(', '));
    } else {
      fail(check, details.join(', '));
    }
  } catch (err) {
    fail(check, `Error: ${err.message}`);
  }
}

async function checkHeroImageSizes() {
  const check = 'Hero Image Sizes';
  try {
    const files = await readdir(IMAGES_DIR);
    const heroes = files.filter(f => f.startsWith('landing-hero'));

    if (heroes.length === 0) {
      pass(check, 'No hero images found');
      return;
    }

    let allGood = true;
    const details = [];
    for (const hero of heroes) {
      const filePath = join(IMAGES_DIR, hero);
      const st = await stat(filePath);
      const sizeKb = Math.round(st.size / 1024);
      if (sizeKb > HERO_MAX_KB) {
        details.push(`${hero}: ${sizeKb}KB (max ${HERO_MAX_KB}KB)`);
        allGood = false;
      } else {
        details.push(`${hero}: ${sizeKb}KB ✓`);
      }
    }

    if (allGood) {
      pass(check, details.join(', '));
    } else {
      fail(check, details.join(', '));
    }
  } catch (err) {
    fail(check, `Error: ${err.message}`);
  }
}

async function checkForceDynamic() {
  const check = 'Root Layout force-dynamic';
  try {
    const content = await readFile(ROOT_LAYOUT, 'utf-8');
    if (content.includes('force-dynamic')) {
      fail(check, 'Root layout contains "force-dynamic" — this makes all pages dynamic');
    } else {
      pass(check, 'No force-dynamic found in root layout');
    }
  } catch (err) {
    fail(check, `Error reading ${ROOT_LAYOUT}: ${err.message}`);
  }
}

async function checkBuildRoutes() {
  const check = 'Build Route Analysis';
  const routesManifest = join(FRONTEND_DIR, '.next/routes-manifest.json');

  if (!existsSync(routesManifest)) {
    results.push({ check, status: 'ℹ️ SKIP', detail: 'No build output found. Run `npm run build` first.' });
    return;
  }

  try {
    const manifest = JSON.parse(await readFile(routesManifest, 'utf-8'));
    const staticRoutes = manifest.staticRoutes?.length ?? 0;
    const dynamicRoutes = manifest.dynamicRoutes?.length ?? 0;

    pass(check, `Static: ${staticRoutes}, Dynamic: ${dynamicRoutes}`);
  } catch (err) {
    results.push({ check, status: 'ℹ️ SKIP', detail: `Error parsing routes manifest: ${err.message}` });
  }
}

// Run all checks
await checkSvgSizes();
await checkHeroImageSizes();
await checkForceDynamic();
await checkBuildRoutes();

// Print report
console.log('\n╔══════════════════════════════════════════╗');
console.log('║       PERFORMANCE BUDGET REPORT          ║');
console.log('╚══════════════════════════════════════════╝\n');

const maxCheckLen = Math.max(...results.map(r => r.check.length));
for (const r of results) {
  console.log(`  ${r.status}  ${r.check.padEnd(maxCheckLen + 2)}${r.detail}`);
}

const failures = results.filter(r => r.status === '❌ FAIL');
console.log('');

if (failures.length > 0) {
  console.log(`\n❌ ${failures.length} check(s) failed!\n`);
  process.exit(1);
} else {
  console.log(`\n✅ All checks passed!\n`);
  process.exit(0);
}
