#!/usr/bin/env node
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';

const rootDir = process.cwd();
const controllersDir = resolve(rootDir, 'backend/src/NaderGorge.API/Controllers');
const frontendScanDirectories = [
  'frontend/src/services',
  'frontend/src/app/api',
  'frontend/src/components',
  'frontend/src/packages',
];
const jsonPath = resolve(rootDir, 'tests/endpoint_inventory.json');
const markdownPath = resolve(rootDir, 'tests/endpoint_inventory.md');
const checkOnly = process.argv.includes('--check');
const apiBaseIdentifiers = new Set(['API_URL', 'API_BASE_URL', 'INTERNAL_API_URL', 'NEXT_PUBLIC_API_URL']);

function normalizeSlashes(value) {
  return value.replace(/\\/g, '/').replace(/\/+/g, '/');
}

function controllerToken(controllerName) {
  return controllerName.replace(/Controller$/, '').toLowerCase();
}

function stripRouteConstraints(route) {
  return route.replace(/\{([^}:]+):[^}]+\}/g, '{$1}');
}

function normalizeRoute(route, controllerName) {
  const replaced = route
    .replace(/\[controller\]/gi, controllerToken(controllerName))
    .replace(/\[action\]/gi, '')
    .replace(/^~/, '');

  return stripRouteConstraints(normalizeSlashes(replaced)).replace(/^\/|\/$/g, '');
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

function extractControllerClass(parseSource) {
  const classMatch = parseSource.match(/\bpublic\s+class\s+(\w+Controller)\b/);
  if (!classMatch) {
    return {
      controllerName: '',
      classAttributes: '',
    };
  }

  const linesBeforeClass = parseSource.slice(0, classMatch.index).split('\n');
  const attributeLines = [];

  for (let index = linesBeforeClass.length - 1; index >= 0; index -= 1) {
    const line = linesBeforeClass[index];
    const trimmed = line.trim();
    if (!trimmed) {
      if (attributeLines.length === 0) {
        continue;
      }
      break;
    }

    if (!trimmed.startsWith('[')) {
      break;
    }

    attributeLines.unshift(line);
  }

  return {
    controllerName: classMatch[1],
    classAttributes: attributeLines.join('\n'),
  };
}

function classifyAuthorization(classAttributes, methodAttributes) {
  const customAttributes = `${classAttributes}\n${methodAttributes}`;
  if (/\[InternalTokenAuthorize(?:Attribute)?\b/i.test(customAttributes)) {
    return 'internal-token';
  }

  if (/\[E2eOnly(?:Attribute)?\b/i.test(customAttributes)) {
    return 'e2e-token';
  }

  const methodAllowsAnonymous = /\[AllowAnonymous(?:Attribute)?\b/i.test(methodAttributes);
  if (methodAllowsAnonymous) {
    return 'anonymous';
  }

  const methodRequiresAuthorization = /\[Authorize(?:Attribute)?\b/i.test(methodAttributes);
  const classRequiresAuthorization = /\[Authorize(?:Attribute)?\b/i.test(classAttributes);

  if (methodRequiresAuthorization || classRequiresAuthorization) {
    return 'authorized';
  }

  const classAllowsAnonymous = /\[AllowAnonymous(?:Attribute)?\b/i.test(classAttributes);
  if (classAllowsAnonymous) {
    return 'anonymous';
  }

  return 'anonymous';
}

function parseController(filePath) {
  const source = readFileSync(filePath, 'utf8');
  const parseSource = source.replace(/\/\/[^\n]*/g, (comment) => ' '.repeat(comment.length));
  const controllerClass = extractControllerClass(parseSource);
  const controllerName = controllerClass.controllerName || filePath.split('/').pop().replace(/\.cs$/, '');
  const classAttributes = controllerClass.classAttributes;
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
    const routeTemplate = combineRoutes(route, methodRoute, controllerName);
    const path = routeTemplate.toLowerCase();

    endpoints.push({
      controller: controllerName,
      action: actionName,
      method,
      path,
      routeTemplate,
      authorization: classifyAuthorization(classAttributes, attributes),
      source: {
        file: relativeFile,
        line: lineNumberAt(source, httpAttributeIndex),
      },
    });
  }

  return endpoints;
}

function collectFrontendFiles(directory) {
  const absoluteDirectory = resolve(rootDir, directory);
  if (!existsSync(absoluteDirectory)) {
    return [];
  }

  const entries = readdirSync(absoluteDirectory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    if (entry.name === 'node_modules' || entry.name === '.next' || entry.name === 'dist') {
      continue;
    }

    const absolutePath = resolve(absoluteDirectory, entry.name);
    if (entry.isDirectory()) {
      files.push(...collectFrontendFiles(normalizeSlashes(relative(rootDir, absolutePath))));
      continue;
    }

    if (entry.isFile() && /\.(ts|tsx)$/.test(entry.name) && !entry.name.endsWith('.d.ts')) {
      files.push(absolutePath);
    }
  }

  return files;
}

function maskComments(source) {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, (comment) => comment.replace(/[^\n]/g, ' '))
    .replace(/\/\/[^\n]*/g, (comment) => ' '.repeat(comment.length));
}

function skipWhitespace(source, index) {
  let cursor = index;
  while (cursor < source.length && /\s/.test(source[cursor])) {
    cursor += 1;
  }
  return cursor;
}

function skipGenericArguments(source, index) {
  let cursor = skipWhitespace(source, index);
  if (source[cursor] !== '<') {
    return cursor;
  }

  let depth = 0;
  while (cursor < source.length) {
    const character = source[cursor];
    if (character === '<') {
      depth += 1;
    } else if (character === '>') {
      depth -= 1;
      if (depth === 0) {
        cursor += 1;
        break;
      }
    }
    cursor += 1;
  }

  return skipWhitespace(source, cursor);
}

function findMatchingDelimiter(source, openIndex, openCharacter, closeCharacter) {
  let depth = 0;
  let quote = '';
  let escaped = false;

  for (let index = openIndex; index < source.length; index += 1) {
    const character = source[index];

    if (quote) {
      if (escaped) {
        escaped = false;
        continue;
      }

      if (character === '\\') {
        escaped = true;
        continue;
      }

      if (character === quote) {
        quote = '';
      }
      continue;
    }

    if (character === '"' || character === "'" || character === '`') {
      quote = character;
      continue;
    }

    if (character === openCharacter) {
      depth += 1;
      continue;
    }

    if (character === closeCharacter) {
      depth -= 1;
      if (depth === 0) {
        return index;
      }
    }
  }

  return -1;
}

function splitTopLevelArguments(argumentsText) {
  const args = [];
  let start = 0;
  let curlyDepth = 0;
  let squareDepth = 0;
  let parenDepth = 0;
  let quote = '';
  let escaped = false;

  for (let index = 0; index < argumentsText.length; index += 1) {
    const character = argumentsText[index];

    if (quote) {
      if (escaped) {
        escaped = false;
        continue;
      }
      if (character === '\\') {
        escaped = true;
        continue;
      }
      if (character === quote) {
        quote = '';
      }
      continue;
    }

    if (character === '"' || character === "'" || character === '`') {
      quote = character;
      continue;
    }

    if (character === '{') curlyDepth += 1;
    if (character === '}') curlyDepth -= 1;
    if (character === '[') squareDepth += 1;
    if (character === ']') squareDepth -= 1;
    if (character === '(') parenDepth += 1;
    if (character === ')') parenDepth -= 1;

    if (character === ',' && curlyDepth === 0 && squareDepth === 0 && parenDepth === 0) {
      args.push(argumentsText.slice(start, index).trim());
      start = index + 1;
    }
  }

  const finalArgument = argumentsText.slice(start).trim();
  if (finalArgument) {
    args.push(finalArgument);
  }

  return args;
}

function literalContent(argument) {
  const trimmed = argument.trim();
  const quote = trimmed[0];
  if (quote !== "'" && quote !== '"' && quote !== '`') {
    return null;
  }

  let escaped = false;
  for (let index = 1; index < trimmed.length; index += 1) {
    const character = trimmed[index];
    if (escaped) {
      escaped = false;
      continue;
    }
    if (character === '\\') {
      escaped = true;
      continue;
    }
    if (character === quote) {
      return {
        quote,
        content: trimmed.slice(1, index),
      };
    }
  }

  return null;
}

function placeholderName(expression) {
  const cleaned = expression
    .replace(/^encodeURIComponent\s*\(/, '')
    .replace(/\)$/g, '')
    .replace(/\.toString\s*\(\s*\)$/g, '')
    .trim();
  const identifiers = cleaned.match(/[A-Za-z_$][\w$]*/g) ?? [];
  const ignored = new Set(['encodeURIComponent', 'String', 'Number', 'Date', 'toString', 'replace']);
  const meaningful = identifiers.filter((identifier) => !ignored.has(identifier));
  return meaningful.at(-1) ?? 'param';
}

function extractQueryParametersFromContent(content) {
  const queryStart = content.indexOf('?');
  if (queryStart === -1) {
    return [];
  }

  const queryText = content.slice(queryStart + 1);
  const parameters = [];

  for (const segment of queryText.split('&')) {
    const match = segment.match(/^([A-Za-z0-9_.-]+)=/);
    if (match) {
      parameters.push(match[1]);
    }
  }

  if (parameters.length === 0 && queryText.trim()) {
    const dynamicName = queryText.match(/\$\{([^}]+)\}/)?.[1];
    parameters.push(dynamicName ? `dynamic:${placeholderName(dynamicName)}` : 'dynamic');
  }

  return parameters;
}

function splitDynamicQuerySuffix(content) {
  const match = content.match(/^(.*)\$\{([^}]*query[^}]*)\}$/i);
  if (!match || match[1].endsWith('/')) {
    return {
      content,
      queryParameters: [],
    };
  }

  return {
    content: match[1],
    queryParameters: [`dynamic:${placeholderName(match[2])}`],
  };
}

function replaceTemplateExpressions(path) {
  return path.replace(/\$\{([^}]+)\}/g, (_, expression) => `{${placeholderName(expression)}}`);
}

function removeKnownApiBase(content) {
  const baseMatch = content.match(/^\$\{([^}]+)\}/);
  if (baseMatch && apiBaseIdentifiers.has(baseMatch[1].trim())) {
    return {
      content: content.slice(baseMatch[0].length),
      matchedApiBase: true,
    };
  }

  return {
    content,
    matchedApiBase: false,
  };
}

function classifyFetchOrigin(content, matchedApiBase) {
  if (matchedApiBase) {
    return 'backend-api';
  }

  if (content.startsWith('${WORKER_URL}') || content.startsWith('targetUrl')) {
    return 'worker-api';
  }

  if (content.startsWith('/api/worker') || content.startsWith('/api/video') || content.startsWith('/api/qr')) {
    return 'next-api';
  }

  if (content.startsWith('/api/')) {
    return 'next-api';
  }

  if (/^https?:\/\//i.test(content)) {
    return 'external';
  }

  return 'external';
}

function normalizeFrontendPath(argument, callerKind) {
  const literal = literalContent(argument);
  if (!literal) {
    const dynamicName = argument.trim().split(/\s+/)[0] || '<dynamic>';
    return {
      origin: dynamicName === 'targetUrl' ? 'worker-api' : 'external',
      path: '<dynamic>',
      routeTemplate: '<dynamic>',
      queryParameters: [],
      rawExpression: argument.trim(),
    };
  }

  const originalContent = literal.content;
  const apiBase = removeKnownApiBase(originalContent);
  const queryAwareContent = splitDynamicQuerySuffix(apiBase.content);
  const origin = callerKind === 'apiClient' || callerKind === 'axios'
    ? 'backend-api'
    : classifyFetchOrigin(queryAwareContent.content, apiBase.matchedApiBase);
  const queryParameters = [
    ...extractQueryParametersFromContent(queryAwareContent.content),
    ...queryAwareContent.queryParameters,
  ];
  const pathWithoutQuery = queryAwareContent.content.split('?')[0] || '/';
  const templatedPath = replaceTemplateExpressions(pathWithoutQuery);
  const normalizedPath = normalizeSlashes(templatedPath.startsWith('/') ? templatedPath : `/${templatedPath}`);
  const backendPath = normalizedPath.startsWith('/api/')
    ? normalizedPath
    : `/api${normalizedPath}`;
  const path = origin === 'backend-api' ? backendPath : normalizedPath;

  return {
    origin,
    path: stripRouteConstraints(path).toLowerCase(),
    routeTemplate: stripRouteConstraints(path),
    queryParameters,
    rawExpression: argument.trim(),
  };
}

function extractParamsObjectKeys(argumentsText) {
  const paramsMatch = /\bparams\s*:\s*/.exec(argumentsText);
  if (!paramsMatch) {
    return [];
  }

  const valueStart = skipWhitespace(argumentsText, paramsMatch.index + paramsMatch[0].length);
  if (argumentsText[valueStart] !== '{') {
    const dynamicValue = argumentsText.slice(valueStart).match(/[A-Za-z_$][\w$]*/)?.[0];
    return dynamicValue ? [`dynamic:${dynamicValue}`] : ['dynamic'];
  }

  const valueEnd = findMatchingDelimiter(argumentsText, valueStart, '{', '}');
  if (valueEnd === -1) {
    return ['dynamic'];
  }

  const objectText = argumentsText.slice(valueStart + 1, valueEnd);
  return splitTopLevelArguments(objectText)
    .map((entry) => {
      const cleaned = entry.trim();
      if (!cleaned || cleaned.startsWith('...')) {
        return null;
      }
      return cleaned.includes(':') ? cleaned.split(':')[0].trim() : cleaned.match(/[A-Za-z_$][\w$]*/)?.[0];
    })
    .filter(Boolean);
}

function oneLine(value) {
  return value.replace(/\s+/g, ' ').trim();
}

function payloadHintFor(method, args) {
  if (!['POST', 'PUT', 'PATCH'].includes(method) || args.length < 2) {
    return '';
  }

  return oneLine(args[1]).slice(0, 120);
}

function payloadFieldsFor(method, args) {
  if (!['POST', 'PUT', 'PATCH'].includes(method) || args.length < 2) {
    return [];
  }

  const payload = args[1].trim();
  if (!payload.startsWith('{')) {
    return [];
  }

  const end = findMatchingDelimiter(payload, 0, '{', '}');
  const objectText = payload.slice(1, end === -1 ? undefined : end);
  return splitTopLevelArguments(objectText)
    .map((entry) => {
      const cleaned = entry.trim();
      if (!cleaned || cleaned.startsWith('...')) {
        return null;
      }
      return cleaned.includes(':') ? cleaned.split(':')[0].trim() : cleaned.match(/[A-Za-z_$][\w$]*/)?.[0];
    })
    .filter(Boolean);
}

function frontendCallContract(filePath, source, method, args, callerKind, callIndex) {
  if (args.length === 0) {
    return null;
  }

  const normalizedPath = normalizeFrontendPath(args[0], callerKind);
  const queryParameters = [
    ...normalizedPath.queryParameters,
    ...extractParamsObjectKeys(args.slice(1).join(', ')),
  ];
  const relativeFile = normalizeSlashes(relative(rootDir, filePath));

  return {
    method,
    path: normalizedPath.path,
    routeTemplate: normalizedPath.routeTemplate,
    origin: normalizedPath.origin,
    queryParameters: [...new Set(queryParameters)],
    payloadHint: payloadHintFor(method, args),
    payloadFields: payloadFieldsFor(method, args),
    rawExpression: normalizedPath.rawExpression,
    source: {
      file: relativeFile,
      line: lineNumberAt(source, callIndex),
    },
  };
}

function extractMethodCalls(filePath, source, callerName) {
  const calls = [];
  const parseSource = maskComments(source);
  const pattern = new RegExp(`\\b${callerName}\\s*\\.\\s*(get|post|put|patch|delete)\\b`, 'gi');
  let match;

  while ((match = pattern.exec(parseSource)) !== null) {
    const method = match[1].toUpperCase();
    const maybeOpenParen = skipGenericArguments(parseSource, match.index + match[0].length);
    if (parseSource[maybeOpenParen] !== '(') {
      continue;
    }

    const closeParen = findMatchingDelimiter(source, maybeOpenParen, '(', ')');
    if (closeParen === -1) {
      continue;
    }

    const argsText = source.slice(maybeOpenParen + 1, closeParen);
    const args = splitTopLevelArguments(argsText);
    const contract = frontendCallContract(filePath, source, method, args, callerName, match.index);
    if (contract) {
      calls.push(contract);
    }
  }

  return calls;
}

function fetchMethodFromOptions(optionsText) {
  const literalMethod = optionsText.match(/\bmethod\s*:\s*['"]([A-Za-z]+)['"]/i)?.[1];
  if (literalMethod) {
    return literalMethod.toUpperCase();
  }

  if (/\bmethod\s*:\s*request\.method\b/.test(optionsText)) {
    return 'ANY';
  }

  return 'GET';
}

function extractFetchCalls(filePath, source) {
  const calls = [];
  const parseSource = maskComments(source);
  const pattern = /\bfetch\s*\(/g;
  let match;

  while ((match = pattern.exec(parseSource)) !== null) {
    const openParen = match.index + match[0].length - 1;
    const closeParen = findMatchingDelimiter(source, openParen, '(', ')');
    if (closeParen === -1) {
      continue;
    }

    const args = splitTopLevelArguments(source.slice(openParen + 1, closeParen));
    const method = args.length > 1 ? fetchMethodFromOptions(args.slice(1).join(', ')) : 'GET';
    const contract = frontendCallContract(filePath, source, method, args, 'fetch', match.index);
    if (contract) {
      calls.push(contract);
    }
  }

  return calls;
}

function parseFrontendFile(filePath) {
  const source = readFileSync(filePath, 'utf8');
  return [
    ...extractMethodCalls(filePath, source, 'apiClient'),
    ...extractMethodCalls(filePath, source, 'axios'),
    ...extractFetchCalls(filePath, source),
  ];
}

function generateFrontendCalls() {
  return frontendScanDirectories
    .flatMap(collectFrontendFiles)
    .flatMap(parseFrontendFile)
    .sort((a, b) => `${a.source.file}:${a.source.line}`.localeCompare(`${b.source.file}:${b.source.line}`));
}

function pathSegments(path) {
  return normalizeSlashes(path)
    .replace(/\?.*$/, '')
    .replace(/^\/|\/$/g, '')
    .split('/')
    .filter(Boolean);
}

function isRouteParameter(segment) {
  return /^\{[^}]+\}$/.test(segment);
}

function routePathsMatch(frontendPath, backendPath) {
  const frontendSegments = pathSegments(stripRouteConstraints(frontendPath.toLowerCase()));
  const backendSegments = pathSegments(stripRouteConstraints(backendPath.toLowerCase()));

  if (frontendSegments.length !== backendSegments.length) {
    return false;
  }

  return frontendSegments.every((segment, index) => {
    const backendSegment = backendSegments[index];
    if (segment === backendSegment) return true;
    if (isRouteParameter(segment) && isRouteParameter(backendSegment)) return true;
    if (segment === '{contenttype}s' && ['packages', 'terms', 'sections'].includes(backendSegment)) return true;
    return false;
  });
}

function backendCandidatesFor(frontendCall, endpoints) {
  return endpoints
    .filter((endpoint) => routePathsMatch(frontendCall.path, endpoint.path))
    .map((endpoint) => ({
      method: endpoint.method,
      path: endpoint.path,
      controller: endpoint.controller,
      action: endpoint.action,
      source: endpoint.source,
    }));
}

function frontendCallMatchesBackend(frontendCall, endpoints) {
  return endpoints.some((endpoint) => (
    endpoint.method === frontendCall.method
    && routePathsMatch(frontendCall.path, endpoint.path)
  ));
}

function generateRouteFindings(frontendCalls, endpoints) {
  return frontendCalls
    .filter((frontendCall) => frontendCall.origin === 'backend-api')
    .filter((frontendCall) => frontendCall.path !== '<dynamic>')
    .filter((frontendCall) => !frontendCall.path.includes('live-support'))
    .filter((frontendCall) => !frontendCallMatchesBackend(frontendCall, endpoints))
    .map((frontendCall) => {
      const candidates = backendCandidatesFor(frontendCall, endpoints);
      const candidateSummary = candidates.length
        ? ` Found same path with method(s): ${candidates.map((candidate) => candidate.method).join(', ')}.`
        : '';

      return {
        severity: 'P1',
        kind: 'missing-backend-route',
        message: `${frontendCall.method} ${frontendCall.path} is called by the frontend but has no matching backend controller route.${candidateSummary}`,
        frontend: frontendCall,
        backendCandidates: candidates,
        status: 'open',
      };
    });
}

function generateInventory() {
  const endpoints = readdirSync(controllersDir)
    .filter((file) => file.endsWith('Controller.cs'))
    .flatMap((file) => parseController(normalizeSlashes(resolve(controllersDir, file))))
    .sort((a, b) => `${a.controller}:${a.path}:${a.method}`.localeCompare(`${b.controller}:${b.path}:${b.method}`));
  const frontendCalls = generateFrontendCalls();
  const routeFindings = generateRouteFindings(frontendCalls, endpoints);

  const digest = createHash('sha256')
    .update(JSON.stringify({
      endpoints: endpoints.map(({ method, path, controller, action }) => ({ method, path, controller, action })),
      frontendCalls: frontendCalls.map(({ method, path, origin, source }) => ({ method, path, origin, source })),
      routeFindings: routeFindings.map(({ kind, message, frontend }) => ({ kind, message, frontend: frontend.source })),
    }))
    .digest('hex');

  return {
    generatedBy: 'scripts/generate-endpoint-inventory.mjs',
    controllerDirectory: 'backend/src/NaderGorge.API/Controllers',
    frontendDirectories: frontendScanDirectories,
    endpointCount: endpoints.length,
    frontendCallCount: frontendCalls.length,
    missingFrontendRouteCount: routeFindings.filter((finding) => finding.kind === 'missing-backend-route').length,
    digest,
    endpoints,
    frontendCalls,
    routeFindings,
  };
}

function renderBackendEndpointsMarkdown(inventory) {
  const grouped = new Map();
  for (const endpoint of inventory.endpoints) {
    const group = grouped.get(endpoint.controller) ?? [];
    group.push(endpoint);
    grouped.set(endpoint.controller, group);
  }

  const lines = ['## Backend Endpoints', ''];

  for (const [controller, endpoints] of grouped.entries()) {
    lines.push(`### ${controller}`, '');
    lines.push('| Method | Path | Action | Auth | Source |');
    lines.push('|---|---|---|---|---|');
    for (const endpoint of endpoints) {
      lines.push(
        `| ${endpoint.method} | \`${endpoint.path}\` | ${endpoint.action} | ${endpoint.authorization} | ${endpoint.source.file}:${endpoint.source.line} |`,
      );
    }
    lines.push('');
  }

  return lines;
}

function renderFrontendCallsMarkdown(inventory) {
  const grouped = new Map();
  for (const call of inventory.frontendCalls) {
    const group = grouped.get(call.source.file) ?? [];
    group.push(call);
    grouped.set(call.source.file, group);
  }

  const lines = ['## Frontend API Calls', ''];

  for (const [file, calls] of grouped.entries()) {
    lines.push(`### ${file}`, '');
    lines.push('| Method | Path | Origin | Query | Payload | Source |');
    lines.push('|---|---|---|---|---|---|');
    for (const call of calls) {
      const query = call.queryParameters.length ? call.queryParameters.join(', ') : '-';
      const payload = call.payloadHint ? call.payloadHint.replace(/\|/g, '\\|') : '-';
      lines.push(
        `| ${call.method} | \`${call.path}\` | ${call.origin} | ${query} | \`${payload}\` | ${call.source.file}:${call.source.line} |`,
      );
    }
    lines.push('');
  }

  return lines;
}

function renderRouteFindingsMarkdown(inventory) {
  const lines = ['## Route Findings', ''];

  if (inventory.routeFindings.length === 0) {
    lines.push('No missing frontend-called backend routes.', '');
    return lines;
  }

  lines.push('| Severity | Kind | Message | Frontend Source |');
  lines.push('|---|---|---|---|');
  for (const finding of inventory.routeFindings) {
    lines.push(
      `| ${finding.severity} | ${finding.kind} | ${finding.message.replace(/\|/g, '\\|')} | ${finding.frontend.source.file}:${finding.frontend.source.line} |`,
    );
  }
  lines.push('');
  return lines;
}

function renderMarkdown(inventory) {
  const lines = [
    '# Backend Endpoint Inventory',
    '',
    `Generated by \`${inventory.generatedBy}\`.`,
    '',
    `Backend endpoint count: **${inventory.endpointCount}**`,
    '',
    `Frontend API call count: **${inventory.frontendCallCount}**`,
    '',
    `Missing frontend route count: **${inventory.missingFrontendRouteCount}**`,
    '',
    `Digest: \`${inventory.digest}\``,
    '',
    ...renderRouteFindingsMarkdown(inventory),
    ...renderFrontendCallsMarkdown(inventory),
    ...renderBackendEndpointsMarkdown(inventory),
  ];

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

  console.log(`Endpoint inventory is current (${inventory.endpointCount} backend endpoints, ${inventory.frontendCallCount} frontend calls).`);
  process.exit(0);
}

writeFileSync(jsonPath, json);
writeFileSync(markdownPath, markdown);
console.log(`Wrote ${inventory.endpointCount} backend endpoints and ${inventory.frontendCallCount} frontend calls to tests/endpoint_inventory.json and tests/endpoint_inventory.md`);
if (inventory.missingFrontendRouteCount > 0) {
  console.log(`Detected ${inventory.missingFrontendRouteCount} missing frontend-called backend route(s).`);
}
