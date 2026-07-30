#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const sourceRoot = path.join(root, 'src');
const allowlistPath = path.join(root, 'config', 'design-color-allowlist.json');
const checkMode = process.argv.includes('--check');

const COLOR_PATTERN = /(?:#[0-9a-f]{3,8}\b|\b(?:bg|text|border|ring|from|via|to|decoration|outline|shadow|divide|accent|fill|stroke)-(?:slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)(?:-[0-9]{2,3})?\b|\b(?:rgb|rgba|hsl|hsla|oklch)\([^)]*\))/gi;

function walk(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const absolute = path.join(directory, entry.name);
    if (entry.isDirectory()) return walk(absolute);
    return /\.(?:tsx?|css)$/.test(entry.name) ? [absolute] : [];
  });
}

function isAllowlisted(relativePath, rules) {
  return rules.some((rule) => {
    const glob = String(rule.glob ?? '').replaceAll('**', '').replaceAll('*', '');
    return glob && relativePath.includes(glob);
  });
}

const allowlist = JSON.parse(fs.readFileSync(allowlistPath, 'utf8'));
const findings = [];
for (const file of walk(sourceRoot)) {
  const relativePath = path.relative(root, file).replaceAll(path.sep, '/');
  if (isAllowlisted(relativePath, allowlist.entries ?? [])) continue;
  const lines = fs.readFileSync(file, 'utf8').split('\n');
  lines.forEach((line, index) => {
    // Token declarations are the source of truth; only usages outside declarations are reported.
    const declarationOnly = line.includes('--') && line.includes(':');
    if (declarationOnly) return;
    for (const match of line.matchAll(COLOR_PATTERN)) {
      findings.push({ file: relativePath, line: index + 1, value: match[0] });
    }
  });
}

if (findings.length === 0) {
  console.log('Design token check passed: no unallowlisted raw color usage found.');
  process.exit(0);
}

console.log(`Design token report: ${findings.length} unallowlisted color usages found.`);
for (const finding of findings.slice(0, 120)) {
  console.log(`- ${finding.file}:${finding.line} ${finding.value}`);
}
if (findings.length > 120) console.log(`... ${findings.length - 120} more`);
if (checkMode) process.exit(1);
