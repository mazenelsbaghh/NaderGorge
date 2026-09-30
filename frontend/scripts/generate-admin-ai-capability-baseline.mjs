#!/usr/bin/env node
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import ts from '../node_modules/typescript/lib/typescript.js';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const frontendRoot = resolve(scriptDirectory, '..');
const repositoryRoot = resolve(frontendRoot, '..');
const sourceRoot = resolve(frontendRoot, 'src');
const outputPath = resolve(repositoryRoot, 'tests/admin_ai_frontend_reachable_calls.json');
const checkOnly = process.argv.includes('--check');
const sourceExtensions = ['.ts', '.tsx', '.js', '.jsx', '.mts', '.cts'];

function normalize(value) {
  return value.replace(/\\/g, '/');
}

function walk(directory) {
  if (!existsSync(directory)) return [];
  return readdirSync(directory, { withFileTypes: true })
    .flatMap((entry) => {
      const fullPath = resolve(directory, entry.name);
      if (entry.isDirectory()) {
        return ['node_modules', '.next', 'dist'].includes(entry.name) ? [] : walk(fullPath);
      }
      return entry.isFile() && sourceExtensions.some((extension) => entry.name.endsWith(extension))
        ? [fullPath]
        : [];
    });
}

function fileCandidates(basePath) {
  return [
    ...sourceExtensions.map((extension) => `${basePath}${extension}`),
    ...sourceExtensions.map((extension) => resolve(basePath, `index${extension}`)),
  ];
}

function resolveImport(fromFile, specifier) {
  if (!specifier.startsWith('.') && !specifier.startsWith('@/')) return null;
  const basePath = specifier.startsWith('@/')
    ? resolve(sourceRoot, specifier.slice(2))
    : resolve(dirname(fromFile), specifier);
  return fileCandidates(basePath).find(existsSync) ?? null;
}

function importSpecifiers(sourceFile) {
  const imports = [];
  sourceFile.forEachChild((node) => {
    if ((ts.isImportDeclaration(node) || ts.isExportDeclaration(node)) && node.moduleSpecifier && ts.isStringLiteral(node.moduleSpecifier)) {
      imports.push(node.moduleSpecifier.text);
    }
  });
  return imports;
}

function routeForPage(filePath) {
  const relativePath = normalize(relative(resolve(sourceRoot, 'app/admin'), filePath));
  if (!/(^|\/)page\.(tsx?|jsx?)$/.test(relativePath)) return null;
  const segments = dirname(relativePath).split('/').filter((segment) => segment && segment !== '.')
    .map((segment) => segment.replace(/^\[\.\.\.(.+)\]$/, '*').replace(/^\[(.+)\]$/, ':$1'));
  return `/admin${segments.length ? `/${segments.join('/')}` : ''}`;
}

function literalPath(node) {
  if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) return node.text;
  if (ts.isTemplateExpression(node)) {
    const names = node.templateSpans.map((span) => {
      const expression = span.expression;
      if (ts.isIdentifier(expression)) return expression.text;
      if (ts.isPropertyAccessExpression(expression)) return expression.name.text;
      return expression.getText().replace(/[^A-Za-z0-9_$]/g, '') || 'value';
    });
    return `${node.head.text}${names.map((name, index) => `{${name}}${node.templateSpans[index].literal.text}`).join('')}`;
  }
  return '<dynamic>';
}

function exportedObject(sourceFile, name) {
  for (const statement of sourceFile.statements) {
    if (!ts.isVariableStatement(statement) || !statement.modifiers?.some((modifier) => modifier.kind === ts.SyntaxKind.ExportKeyword)) continue;
    for (const declaration of statement.declarationList.declarations) {
      if (ts.isIdentifier(declaration.name) && declaration.name.text === name && declaration.initializer && ts.isObjectLiteralExpression(declaration.initializer)) {
        return declaration.initializer;
      }
    }
  }
  return null;
}

function isTypeOnlyReference(node) {
  for (let parent = node.parent; parent; parent = parent.parent) {
    if (ts.isTypeQueryNode(parent)) return true;
    if (ts.isStatement(parent)) return false;
  }
  return false;
}

export function provenServiceMembers(targetFile, exportName, sourceFiles) {
  let imported = false;
  let uncertain = false;
  const members = new Set();
  for (const [filePath, sourceFile] of sourceFiles) {
    if (filePath === targetFile) continue;
    const aliases = new Set();
    for (const statement of sourceFile.statements) {
      if (!statement.moduleSpecifier || !ts.isStringLiteral(statement.moduleSpecifier)
        || resolveImport(filePath, statement.moduleSpecifier.text) !== targetFile) continue;
      if (ts.isExportDeclaration(statement)) {
        uncertain = true;
        continue;
      }
      if (!ts.isImportDeclaration(statement)) continue;
      const bindings = statement.importClause?.namedBindings;
      if (!bindings || !ts.isNamedImports(bindings)) {
        uncertain = true;
        continue;
      }
      for (const element of bindings.elements) {
        if ((element.propertyName?.text ?? element.name.text) === exportName && !element.isTypeOnly) {
          aliases.add(element.name.text);
          imported = true;
        }
      }
    }
    if (aliases.size === 0) continue;
    const visit = (node) => {
      if (ts.isImportDeclaration(node)) return;
      if (ts.isIdentifier(node) && aliases.has(node.text)) {
        if (isTypeOnlyReference(node)) return;
        const parent = node.parent;
        if (ts.isPropertyAccessExpression(parent) && parent.expression === node) members.add(parent.name.text);
        else if (ts.isElementAccessExpression(parent) && parent.expression === node && ts.isStringLiteral(parent.argumentExpression)) members.add(parent.argumentExpression.text);
        else uncertain = true;
      }
      ts.forEachChild(node, visit);
    };
    visit(sourceFile);
  }
  return imported && !uncertain ? members : null;
}

function containingMember(node, objectLiteral) {
  for (let parent = node.parent; parent && parent !== objectLiteral; parent = parent.parent) {
    if (ts.isPropertyAssignment(parent) && parent.parent === objectLiteral) {
      return ts.isIdentifier(parent.name) || ts.isStringLiteral(parent.name) ? parent.name.text : null;
    }
  }
  return null;
}

function callRecords(filePath, sourceFile, selectedObject = null, selectedMembers = null) {
  const calls = [];
  const visit = (node) => {
    if (ts.isCallExpression(node) && ts.isPropertyAccessExpression(node.expression)) {
      const method = node.expression.name.text.toUpperCase();
      const client = node.expression.expression.getText(sourceFile);
      const member = selectedObject ? containingMember(node, selectedObject) : null;
      if (['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].includes(method) && /(?:apiClient|axios|api)\b/.test(client)
        && (!member || !selectedMembers || selectedMembers.has(member))) {
        const path = node.arguments.length ? literalPath(node.arguments[0]) : '<dynamic>';
        calls.push({
          method,
          path: path.startsWith('/') || path === '<dynamic>' ? path : `/${path}`,
          dynamic: path === '<dynamic>' || path.includes('{'),
          source: {
            file: normalize(relative(repositoryRoot, filePath)),
            line: sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile)).line + 1,
          },
        });
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(sourceFile);
  return calls;
}

export function collectAdminCallGraph() {
  const pageRoots = walk(resolve(sourceRoot, 'app/admin')).filter((filePath) => routeForPage(filePath));
  const roots = [
    ...pageRoots,
    resolve(sourceRoot, 'packages/admin/navigation.tsx'),
    resolve(sourceRoot, 'packages/admin/route-permissions.ts'),
    resolve(sourceRoot, 'components/admin/AdminShellChrome.tsx'),
  ].filter(existsSync);
  const queue = [...new Set(roots)].sort();
  const visited = new Set();
  const reachableFiles = [];
  const sourceFiles = new Map();

  while (queue.length) {
    const filePath = queue.shift();
    if (!filePath || visited.has(filePath)) continue;
    visited.add(filePath);
    const source = readFileSync(filePath, 'utf8');
    const sourceFile = ts.createSourceFile(filePath, source, ts.ScriptTarget.Latest, true);
    sourceFiles.set(filePath, sourceFile);
    reachableFiles.push({ file: normalize(relative(repositoryRoot, filePath)), route: routeForPage(filePath) });
    for (const specifier of importSpecifiers(sourceFile)) {
      const imported = resolveImport(filePath, specifier);
      if (imported && !visited.has(imported)) queue.push(imported);
    }
    queue.sort();
  }

  const serviceSelections = new Map();
  const servicesDirectory = `${resolve(sourceRoot, 'services')}/`;
  for (const [filePath, sourceFile] of sourceFiles) {
    if (!filePath.startsWith(servicesDirectory)) continue;
    const exportedServices = sourceFile.statements
      .filter((statement) => ts.isVariableStatement(statement)
        && statement.modifiers?.some((modifier) => modifier.kind === ts.SyntaxKind.ExportKeyword))
      .flatMap((statement) => statement.declarationList.declarations)
      .filter((declaration) => ts.isIdentifier(declaration.name)
        && /Service$/.test(declaration.name.text)
        && declaration.initializer && ts.isObjectLiteralExpression(declaration.initializer))
      .map((declaration) => declaration.name.text);
    // Multiple service objects in one file need separate call ownership analysis.
    // Until then, keep every call from that file in the conservative inventory.
    if (exportedServices.length !== 1) continue;
    const exportName = exportedServices[0];
    const object = exportedObject(sourceFile, exportName);
    const members = object && provenServiceMembers(filePath, exportName, sourceFiles);
    if (object && members) serviceSelections.set(filePath, { object, members });
  }
  const calls = [...sourceFiles].flatMap(([filePath, sourceFile]) => callRecords(
    filePath,
    sourceFile,
    serviceSelections.get(filePath)?.object,
    serviceSelections.get(filePath)?.members,
  ));

  const normalizedCalls = calls
    .sort((left, right) => JSON.stringify(left).localeCompare(JSON.stringify(right)))
    .filter((call, index, list) => index === 0 || JSON.stringify(call) !== JSON.stringify(list[index - 1]));
  const unreachableCalls = [
    ...walk(resolve(sourceRoot, 'services')),
    ...walk(resolve(sourceRoot, 'app/api')),
  ]
    .filter((filePath) => !visited.has(filePath))
    .flatMap((filePath) => callRecords(
      filePath,
      ts.createSourceFile(filePath, readFileSync(filePath, 'utf8'), ts.ScriptTarget.Latest, true),
    ))
    .sort((left, right) => JSON.stringify(left).localeCompare(JSON.stringify(right)));
  const payload = {
    schemaVersion: 1,
    root: 'frontend/src/app/admin',
    reachableFileCount: reachableFiles.length,
    reachableFiles: reachableFiles.sort((left, right) => left.file.localeCompare(right.file)),
    callCount: normalizedCalls.length,
    calls: normalizedCalls,
    unreachableCallCount: unreachableCalls.length,
    unreachableCalls,
  };
  payload.digest = createHash('sha256').update(JSON.stringify(payload)).digest('hex');
  return payload;
}

export function generate() {
  const payload = collectAdminCallGraph();
  const serialized = `${JSON.stringify(payload, null, 2)}\n`;
  if (checkOnly) {
    if (!existsSync(outputPath) || readFileSync(outputPath, 'utf8') !== serialized) {
      throw new Error('AdminAI reachable frontend call graph is stale. Run: node frontend/scripts/generate-admin-ai-capability-baseline.mjs');
    }
    return payload;
  }
  writeFileSync(outputPath, serialized);
  return payload;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const payload = generate();
  process.stdout.write(`AdminAI frontend call graph is current (${payload.reachableFileCount} files, ${payload.callCount} calls).\n`);
}
