#!/usr/bin/env node
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';

const rootDir = process.cwd();
const controllersDir = resolve(rootDir, 'backend/src/NaderGorge.API/Controllers');
const jsonPath = resolve(rootDir, 'tests/endpoint_inventory.json');
const markdownPath = resolve(rootDir, 'tests/endpoint_inventory.md');
const checkOnly = process.argv.includes('--check');

function normalizeSlashes(value) {
  return value.replace(/\\/g, '/').replace(/\/+/g, '/');
}

function controllerToken(controllerName) {
  return controllerName.replace(/Controller$/, '').toLowerCase();
}

function normalizeRoute(route, controllerName) {
  const replaced = route
    .replace(/\[controller\]/gi, controllerToken(controllerName))
    .replace(/\[action\]/gi, '')
    .replace(/^~/, '');

  return normalizeSlashes(replaced).replace(/^\/|\/$/g, '');
}

function combineRoutes(classRoute, methodRoute, controllerName) {
  const normalizedClassRoute = normalizeRoute(classRoute || '', controllerName);
  const normalizedMethodRoute = normalizeRoute(methodRoute || '', controllerName);
  const combined = normalizeSlashes(`/${normalizedClassRoute}/${normalizedMethodRoute}`).replace(/\/$/g, '');
  return combined || '/';
}

function lineNumberAt(source, index) {
  return source.slice(0, index).split('\n').length;
}

function extractAttributeValue(attributeText, attributeName) {
  const pattern = new RegExp(`\\[${attributeName}(?:Attribute)?\\s*\\(\\s*"([^"]*)"`, 'i');
  return attributeText.match(pattern)?.[1] ?? '';
}

function classifyAuthorization(classAttributes, methodAttributes, source, signatureStart) {
  const methodAllowsAnonymous = /\[AllowAnonymous(?:Attribute)?\b/i.test(methodAttributes);
  if (methodAllowsAnonymous) {
    return 'anonymous';
  }

  const classAllowsAnonymous = /\[AllowAnonymous(?:Attribute)?\b/i.test(classAttributes);
  const methodRequiresAuthorization = /\[Authorize(?:Attribute)?\b/i.test(methodAttributes);
  const classRequiresAuthorization = /\[Authorize(?:Attribute)?\b/i.test(classAttributes);
  const precedingSlice = source.slice(Math.max(0, signatureStart - 1200), signatureStart);

  if (/X-Internal-Token|ServiceToken|InternalToken|API_CALLBACK_SECRET/i.test(precedingSlice)) {
    return 'internal-token';
  }

  if (methodRequiresAuthorization || classRequiresAuthorization) {
    return 'authorized';
  }

  if (classAllowsAnonymous) {
    return 'anonymous';
  }

  return 'anonymous';
}

function parseController(filePath) {
  const source = readFileSync(filePath, 'utf8');
  const parseSource = source.replace(/\/\/[^\n]*/g, (comment) => ' '.repeat(comment.length));
  const controllerName = filePath.split('/').pop().replace(/\.cs$/, '');
  const classMatch = parseSource.match(/((?:\s*\[[^\]]+\]\s*)*)\s*public\s+class\s+(\w+Controller)\b/);
  const classAttributes = classMatch?.[1] ?? '';
  const route = extractAttributeValue(classAttributes, 'Route') || `api/${controllerToken(controllerName)}`;
  const methodPattern = /((?:\s*\[[^\]]+\]\s*)+)\s*public\s+(?:async\s+)?(?:[\w<>,\[\]\s?.]+)\s+(\w+)\s*\(/g;
  const endpoints = [];
  let match;

  while ((match = methodPattern.exec(parseSource)) !== null) {
    const attributes = match[1];
    const actionName = match[2];
    const httpMatch = attributes.match(/\[(HttpGet|HttpPost|HttpPut|HttpDelete|HttpPatch)(?:Attribute)?(?:\s*\(\s*"([^"]*)")?/i);

    if (!httpMatch) {
      continue;
    }

    const method = httpMatch[1].replace(/^Http/i, '').toUpperCase();
    const methodRoute = httpMatch[2] ?? '';
    const httpAttributeIndex = match.index + match[1].search(/\[Http/i);
    const relativeFile = normalizeSlashes(relative(rootDir, filePath));
    const path = combineRoutes(route, methodRoute, controllerName);

    endpoints.push({
      controller: controllerName,
      action: actionName,
      method,
      path: path.toLowerCase(),
      routeTemplate: combineRoutes(route, methodRoute, controllerName),
      authorization: classifyAuthorization(classAttributes, attributes, source, match.index),
      source: {
        file: relativeFile,
        line: lineNumberAt(source, httpAttributeIndex),
      },
    });
  }

  return endpoints;
}

function generateInventory() {
  const endpoints = readdirSync(controllersDir)
    .filter((file) => file.endsWith('Controller.cs'))
    .flatMap((file) => parseController(normalizeSlashes(resolve(controllersDir, file))))
    .sort((a, b) => `${a.controller}:${a.path}:${a.method}`.localeCompare(`${b.controller}:${b.path}:${b.method}`));

  const digest = createHash('sha256')
    .update(JSON.stringify(endpoints.map(({ method, path, controller, action }) => ({ method, path, controller, action }))))
    .digest('hex');

  return {
    generatedBy: 'scripts/generate-endpoint-inventory.mjs',
    controllerDirectory: 'backend/src/NaderGorge.API/Controllers',
    endpointCount: endpoints.length,
    digest,
    endpoints,
  };
}

function renderMarkdown(inventory) {
  const grouped = new Map();
  for (const endpoint of inventory.endpoints) {
    const group = grouped.get(endpoint.controller) ?? [];
    group.push(endpoint);
    grouped.set(endpoint.controller, group);
  }

  const lines = [
    '# Backend Endpoint Inventory',
    '',
    `Generated by \`${inventory.generatedBy}\`.`,
    '',
    `Endpoint count: **${inventory.endpointCount}**`,
    '',
    `Digest: \`${inventory.digest}\``,
    '',
  ];

  for (const [controller, endpoints] of grouped.entries()) {
    lines.push(`## ${controller}`, '');
    lines.push('| Method | Path | Action | Auth | Source |');
    lines.push('|---|---|---|---|---|');
    for (const endpoint of endpoints) {
      lines.push(
        `| ${endpoint.method} | \`${endpoint.path}\` | ${endpoint.action} | ${endpoint.authorization} | ${endpoint.source.file}:${endpoint.source.line} |`,
      );
    }
    lines.push('');
  }

  return `${lines.join('\n')}\n`;
}

const inventory = generateInventory();
const json = `${JSON.stringify(inventory, null, 2)}\n`;
const markdown = renderMarkdown(inventory);

if (checkOnly) {
  const existingJson = existsSync(jsonPath) ? readFileSync(jsonPath, 'utf8') : '';
  const existingMarkdown = existsSync(markdownPath) ? readFileSync(markdownPath, 'utf8') : '';

  if (existingJson !== json || existingMarkdown !== markdown) {
    console.error('Endpoint inventory is stale. Run: node scripts/generate-endpoint-inventory.mjs');
    process.exit(1);
  }

  console.log(`Endpoint inventory is current (${inventory.endpointCount} endpoints).`);
  process.exit(0);
}

writeFileSync(jsonPath, json);
writeFileSync(markdownPath, markdown);
console.log(`Wrote ${inventory.endpointCount} endpoints to tests/endpoint_inventory.json and tests/endpoint_inventory.md`);
