#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const root = process.cwd();
const sourceRoot = path.join(root, 'src');
const allowlistPath = path.join(root, 'config', 'design-color-allowlist.json');
const checkMode = process.argv.includes('--check');

const COLOR_PATTERN = /(?:#[0-9a-f]{3,8}\b|\b(?:bg|text|border|ring|from|via|to|decoration|outline|shadow|divide|accent|fill|stroke)-(?:slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)(?:-[0-9]{2,3})?\b|\b(?:rgb|rgba|hsl|hsla|oklch)\([^)]*\))/gi;
const INLINE_EXCEPTION = 'design-token-allow:';

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
const allowlistEntries = allowlist.entries ?? [];
const findings = [];
for (const file of walk(sourceRoot)) {
  const relativePath = path.relative(root, file).replaceAll(path.sep, '/');
  if (isAllowlisted(relativePath, allowlistEntries)) continue;
  const lines = fs.readFileSync(file, 'utf8').split('\n');
  lines.forEach((line, index) => {
    // Token declarations are the source of truth; only usages outside declarations are reported.
    const declarationOnly = line.includes('--') && line.includes(':');
    if (declarationOnly || line.includes(INLINE_EXCEPTION)) return;
    for (const match of line.matchAll(COLOR_PATTERN)) {
      findings.push({ file: relativePath, line: index + 1, value: match[0] });
    }
  });
}

function collectNewFindings() {
  const changed = [];
  let currentFile = '';
  let newLine = 0;
  const diff = execFileSync(
    'git',
    ['diff', '--relative', '--find-renames', '--no-ext-diff', '--unified=0', 'HEAD', '--', 'src'],
    { cwd: root, encoding: 'utf8' },
  );
  const deletedSourceLines = new Set(
    diff
      .split('\n')
      .filter((line) => line.startsWith('-') && !line.startsWith('---'))
      .map((line) => line.slice(1)),
  );

  for (const line of diff.split('\n')) {
    if (line.startsWith('+++ b/')) {
      currentFile = line.slice('+++ b/'.length);
      continue;
    }
    const hunk = line.match(/^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@/);
    if (hunk) {
      newLine = Number(hunk[1]);
      continue;
    }
    if (!currentFile || line.startsWith('---')) continue;
    if (line.startsWith('+')) {
      const source = line.slice(1);
      if (
        !deletedSourceLines.has(source) &&
        !isAllowlisted(currentFile, allowlistEntries)
      ) {
        const declarationOnly = source.includes('--') && source.includes(':');
        if (!declarationOnly && !source.includes(INLINE_EXCEPTION)) {
          for (const match of source.matchAll(COLOR_PATTERN)) {
            changed.push({ file: currentFile, line: newLine, value: match[0] });
          }
        }
      }
      newLine += 1;
    } else if (!line.startsWith('-')) {
      newLine += 1;
    }
  }

  const untracked = execFileSync(
    'git',
    ['ls-files', '--others', '--exclude-standard', '--', 'src'],
    { cwd: root, encoding: 'utf8' },
  )
    .split('\n')
    .filter((file) => /\.(?:tsx?|css)$/.test(file));

  for (const relativePath of untracked) {
    if (isAllowlisted(relativePath, allowlistEntries)) continue;
    const lines = fs.readFileSync(path.join(root, relativePath), 'utf8').split('\n');
    lines.forEach((line, index) => {
      if (deletedSourceLines.has(line)) return;
      const declarationOnly = line.includes('--') && line.includes(':');
      if (declarationOnly || line.includes(INLINE_EXCEPTION)) return;
      for (const match of line.matchAll(COLOR_PATTERN)) {
        changed.push({ file: relativePath, line: index + 1, value: match[0] });
      }
    });
  }

  return changed;
}

if (checkMode) {
  const newFindings = collectNewFindings();
  if (newFindings.length === 0) {
    console.log('Design token gate passed: no new raw color usage found.');
    process.exit(0);
  }
  console.error(`Design token gate failed: ${newFindings.length} new raw color usages found.`);
  for (const finding of newFindings.slice(0, 120)) {
    console.error(`- ${finding.file}:${finding.line} ${finding.value}`);
  }
  if (newFindings.length > 120) {
    console.error(`... ${newFindings.length - 120} more`);
  }
  process.exit(1);
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
