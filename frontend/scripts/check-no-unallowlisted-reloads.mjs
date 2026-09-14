import fs from 'node:fs';
import path from 'node:path';

const frontendRoot = path.resolve(import.meta.dirname, '..');
const sourceRoot = path.join(frontendRoot, 'src');

// SecureVideoPlayer is the sole documented exception: its reload is part of
// the playback-session recovery/security contract. All other product reloads
// must use targeted state or route refresh mechanisms.
const allowlistedReloadFile = path.join(sourceRoot, 'components/video/SecureVideoPlayer.tsx');
const reloadPattern = /\b(?:window\.)?location\.reload\s*\(/g;
const violations = [];

function visit(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const entryPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      visit(entryPath);
      continue;
    }
    if (!/\.(?:ts|tsx|js|jsx)$/.test(entry.name)) continue;

    const source = fs.readFileSync(entryPath, 'utf8');
    const matches = [...source.matchAll(reloadPattern)];
    if (!matches.length || entryPath === allowlistedReloadFile) continue;
    for (const match of matches) {
      const line = source.slice(0, match.index).split('\n').length;
      violations.push(`${path.relative(frontendRoot, entryPath)}:${line}`);
    }
  }
}

if (!fs.existsSync(allowlistedReloadFile)) {
  console.error(`Reload allowlist target is missing: ${path.relative(frontendRoot, allowlistedReloadFile)}`);
  process.exit(1);
}

visit(sourceRoot);

if (violations.length) {
  console.error('Unallowlisted document reloads found:');
  for (const violation of violations) console.error(`- ${violation}`);
  process.exit(1);
}

console.log(`Reload guard passed. Allowed exception: ${path.relative(frontendRoot, allowlistedReloadFile)}`);
