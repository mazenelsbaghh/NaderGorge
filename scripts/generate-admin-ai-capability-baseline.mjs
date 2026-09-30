#!/usr/bin/env node
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = process.cwd();
const endpointPath = resolve(root, 'tests/endpoint_inventory.json');
const runtimePath = resolve(root, 'tests/admin_ai_runtime_endpoint_inventory.json');
const frontendPath = resolve(root, 'tests/admin_ai_frontend_reachable_calls.json');
const outputPath = resolve(root, 'tests/admin_ai_capability_baseline.json');
const markdownPath = resolve(root, 'tests/admin_ai_capability_baseline.md');
const checkOnly = process.argv.includes('--check');

const strongTerms = /(delete|remove|revoke|reset|password|role|permission|disable|toggle|bulk|finance|payment|wallet|refund|settlement|treasury|expense|salary|payroll|publish|cancel|migrat|transfer|generate)/i;
const externalTerms = /(whatsapp|bunny|upload|export|download|sync|analy[sz]e)/i;
const reviewedStrongRoutes = new Set(['POST:/admin/watch-requests/{}/approve']);
const reviewedSharedAdminCommands = new Map([
  ['POST:/v1/assistant/tasks/my/{}/comments', 'command:AddTaskCommentCommand'],
  ['POST:/v1/assistant/tasks/my/{}/status', 'command:UpdateTaskStatusCommand'],
]);
// These POST handlers only read persisted state or calculate a response. Review
// each handler and its callees before adding another route to this list.
const reviewedReadOnlyPostRoutes = new Map([
  ['POST:/admin/exams/{}/revision-preview', 'preview'],
  ['POST:/admin/homework/{}/revision-preview', 'preview'],
  ['POST:/admin/teacher-finance-center/settlements/preview', 'preview'],
  ['POST:/admin/teacher-finance-center/shared-packages/{}/allocation-preview', 'preview'],
  ['POST:/hr/admin/shifts/assignments/validate', 'read'],
  ['POST:/live-support/whatsapp/campaigns/audience/preview', 'preview'],
  ['POST:/live-support/whatsapp/campaigns/spreadsheet/inspect', 'read'],
  ['POST:/live-support/whatsapp/preferences/contacts/search', 'read'],
]);

function digest(value) {
  return createHash('sha256').update(value).digest('hex');
}

function stable(value) {
  if (Array.isArray(value)) return `[${value.map(stable).join(',')}]`;
  if (value && typeof value === 'object') {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${stable(value[key])}`).join(',')}}`;
  }
  return JSON.stringify(value);
}

function idPart(value) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'root';
}

function routeKey(method, route) {
  const normalized = route.split('?')[0].toLowerCase()
    .replace(/\{[^}]+\}/g, '{}')
    .replace(/^\/api(?=\/)/, '');
  return `${method}:${normalized}`;
}

function domainFor(value) {
  // Preserve CamelCase word boundaries before matching short domain names.
  // Otherwise ApproveWatchRequest contains "hr" across Watch/Request.
  const input = value.replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase();
  if (/(finance|wallet|recharge|payment|treasury|refund|expense|settlement|accounting)/.test(input)) return 'finance';
  if (/(\bhr\b|employee|payroll|leave|recruit|attendance|shift|governance)/.test(input)) return 'hr';
  if (/(student|user|role|auth|device|profile|watch)/.test(input)) return 'identity';
  if (/(content|lesson|video|package|subject|teacher|code|exam|question|homework)/.test(input)) return 'content';
  if (/(gift|sale|coupon|purchase|commercial)/.test(input)) return 'commercial';
  if (/(support|chat|crm|operation)/.test(input)) return 'support';
  if (/(report|log|media|audit)/.test(input)) return 'reporting';
  return 'other';
}

function semantics(method, descriptor, route) {
  const operation = `${descriptor} ${route}`;
  const external = externalTerms.test(operation);
  const reviewedReadEffect = reviewedReadOnlyPostRoutes.get(routeKey(method, route));
  const mutation = method !== 'GET' && method !== 'ANY' && !reviewedReadEffect;
  const effect = reviewedReadEffect ?? (mutation ? (external ? 'external-side-effect' : 'mutation') :
    (/export|download/i.test(operation) ? 'export' : /preview/i.test(operation) ? 'preview' : 'read'));
  const risk = mutation && (method === 'DELETE' || strongTerms.test(operation) || reviewedStrongRoutes.has(routeKey(method, route)))
    ? 'strong' : mutation ? 'ordinary' : 'none';
  return {
    effect,
    risk,
    confirmation: risk === 'strong' ? 'strong' : risk === 'ordinary' ? 'ordinary' : 'none',
    status: mutation ? 'blocked' : 'candidate',
    blocker: mutation ? 'Requires a reviewed authoritative adapter with durable idempotency/recovery, concurrency, audit, and refresh contracts.' : undefined,
  };
}

function includeEndpoint(endpoint) {
  return /^\/api\/admin(?:\/|$)/.test(endpoint.path) ||
    /^\/api\/v1\/assistant\/tasks\/my(?:\/|$)/.test(endpoint.path) ||
    /^\/api\/live-support\/(?:connections|staff)(?:\/|$)/.test(endpoint.path) ||
    /^\/api\/live-support\/whatsapp\/(?:campaigns|preferences|templates)(?:\/|$)/.test(endpoint.path) ||
    /^\/api\/exams\/admin(?:\/|$)/.test(endpoint.path) ||
    /^\/api\/video-learning\/[^/]+\/(?:author|report|ai)$/.test(endpoint.path) ||
    endpoint.controller.startsWith('Admin') ||
    endpoint.controller.startsWith('Hr') ||
    ['CrmController', 'InternalChatController', 'LiveSupportAdminController', 'LiveSupportAIAdminController', 'WhatsAppController'].includes(endpoint.controller) ||
    endpoint.path.startsWith('/api/hr/');
}

function createItem(kind, method, route, source, descriptor, authoritativeOperation) {
  const semantic = semantics(method, descriptor, route);
  const mutation = semantic.risk !== 'none';
  return {
    id: `${kind === 'backend-endpoint' ? 'be' : 'fe'}:${method.toLowerCase()}:${idPart(route)}:${idPart(source.file)}:${source.line}`,
    kind,
    method,
    route,
    effect: semantic.effect,
    domain: domainFor(`${descriptor} ${route}`),
    risk: semantic.risk,
    confirmation: semantic.confirmation,
    status: semantic.status,
    authoritativeOperation,
    inputSchema: `${kind}:${idPart(descriptor)}:input:v1`,
    outputSchema: `${kind}:${idPart(descriptor)}:output:v1`,
    limits: { maxRows: mutation ? 0 : 200, maxBytes: 65536, timeoutMs: 5000 },
    idempotency: mutation ? 'missing' : 'none',
    concurrency: mutation ? 'missing' : 'none',
    audit: mutation ? 'missing' : 'read-evidence',
    refreshScopes: mutation ? [domainFor(`${descriptor} ${route}`)] : [],
    source,
    ...(semantic.blocker ? { blocker: semantic.blocker } : {}),
  };
}

function build() {
  const endpointRaw = readFileSync(endpointPath, 'utf8');
  if (!existsSync(runtimePath)) throw new Error('Missing runtime endpoint snapshot. Run the AdminAIEndpointInventoryTests export first.');
  const runtimeRaw = readFileSync(runtimePath, 'utf8');
  const runtimeKeys = new Set(JSON.parse(runtimeRaw).flatMap((endpoint) =>
    endpoint.methods.map((method) => `${endpoint.controller}.${endpoint.action}:${method}`),
  ));
  const frontendRaw = readFileSync(frontendPath, 'utf8');
  const endpoints = JSON.parse(endpointRaw).endpoints
    .filter(includeEndpoint)
    .filter((endpoint) => runtimeKeys.has(`${endpoint.controller.replace(/Controller$/, '')}.${endpoint.action}:${endpoint.method}`));
  if (!endpoints.length) throw new Error('No diagnostic Admin endpoints matched the authoritative runtime inventory.');
  const frontend = JSON.parse(frontendRaw);
  const backendItems = endpoints.map((endpoint) => createItem(
    'backend-endpoint', endpoint.method, endpoint.path, endpoint.source,
    `${endpoint.controller}.${endpoint.action}`,
    reviewedSharedAdminCommands.get(routeKey(endpoint.method, endpoint.path))
      ?? `diagnostic:${endpoint.controller}.${endpoint.action}`,
  ));
  const backendByRoute = new Map();
  for (const item of backendItems) {
    const key = routeKey(item.method, item.route);
    if (backendByRoute.has(key)) throw new Error(`Ambiguous Admin endpoint route: ${key}`);
    backendByRoute.set(key, item);
  }
  const frontendItems = frontend.calls.map((call) => {
      const backend = backendByRoute.get(routeKey(call.method, call.path));
      const item = createItem('frontend-call', call.method, call.path, call.source,
        call.source.file, backend?.authoritativeOperation ?? 'unresolved:frontend-contract');
      // An exact route points to one authoritative operation. Its effect and risk
      // cannot be downgraded by a generic frontend service filename.
      if (!backend) return item;
      const matched = {
        ...item,
        effect: backend.effect,
        domain: backend.domain,
        risk: backend.risk,
        confirmation: backend.confirmation,
        status: backend.status,
        limits: backend.limits,
        idempotency: backend.idempotency,
        concurrency: backend.concurrency,
        audit: backend.audit,
        refreshScopes: backend.refreshScopes,
      };
      if (backend.blocker) matched.blocker = backend.blocker;
      else delete matched.blocker;
      return matched;
    });
  const selfServicePath = 'frontend/src/services/admin-ai-agent-service.ts';
  const selfServiceItems = frontendItems.filter((item) => item.source.file === selfServicePath);
  const teacherReportsPath = 'frontend/src/services/advanced-report-service.ts';
  const teacherReportItems = frontendItems.filter((item) =>
    item.source.file === teacherReportsPath && item.route.startsWith('/teacher/reports/'));
  const reviewedTeacherCalls = new Map([
    ['frontend/src/services/admin-service.ts', new Set([
      'GET:/teacher/codes/groups', 'GET:/teacher/codes/groups/{id}/details',
    ])],
    ['frontend/src/services/teacher-service.ts', new Set([
      'GET:/teacher/context',
      'GET:/teacher/content/{contentType}/{id}/subscribers',
      'GET:/teacher/content/{contentType}/{id}/subscribers/export',
    ])],
    ['frontend/src/services/finance-service.ts', new Set([
      'GET:/teacher/finance/statement', 'GET:/teacher/finance/statement/pdf',
    ])],
  ]);
  const additionalTeacherItems = frontendItems.filter((item) =>
    reviewedTeacherCalls.get(item.source.file)?.has(`${item.method}:${item.route}`));
  const teacherOnlyItems = [...teacherReportItems, ...additionalTeacherItems];
  const excludedIds = new Set([...selfServiceItems, ...teacherOnlyItems].map((item) => item.id));
  const items = [...backendItems, ...frontendItems.filter((item) => !excludedIds.has(item.id))]
    .sort((left, right) => left.id.localeCompare(right.id));
  const exclusions = [
    ...selfServiceItems.map((item) => ({
      id: item.id,
      reason: 'self-service',
      detail: `Admin AI conversation/proposal transport is not an Admin business capability: ${item.method} ${item.route}`,
    })),
    ...teacherOnlyItems.map((item) => ({
      id: item.id,
      reason: 'teacher-surface',
      detail: `Teacher-only route retained by the shared Admin frontend graph; Admin authority must use its own workflow: ${item.method} ${item.route}`,
    })),
  ].sort((left, right) => left.id.localeCompare(right.id));
  const payload = {
    schemaVersion: 1,
    generatedAtUtc: '2026-08-11T00:00:00.000Z',
    activation: 'blocked',
    sources: {
      runtime: { path: 'tests/admin_ai_runtime_endpoint_inventory.json', digest: digest(runtimeRaw) },
      frontend: { path: 'tests/admin_ai_frontend_reachable_calls.json', digest: digest(frontendRaw) },
      semantic: { path: 'scripts/generate-admin-ai-capability-baseline.mjs', digest: digest(readFileSync(fileURLToPath(import.meta.url))) },
    },
    items,
    exclusions,
  };
  payload.digest = digest(stable(payload));
  return payload;
}

function markdown(payload) {
  const totals = payload.items.reduce((map, item) => {
    map[item.effect] = (map[item.effect] ?? 0) + 1;
    return map;
  }, {});
  return [
    '# Admin AI capability baseline (blocked candidate)',
    '',
    `Digest: \`${payload.digest}\``,
    '',
    `Items: ${payload.items.length}; ${Object.entries(totals).map(([key, count]) => `${key}=${count}`).join(', ')}.`,
    `Reviewed non-business exclusions: ${payload.exclusions.length}.`,
    '',
    'This candidate is intentionally blocked. Every mutation remains blocked until an authoritative command/service adapter, idempotency, concurrency, audit, and confirmation contract are reviewed.',
    '',
    '| ID | Method | Route | Effect | Domain | Risk | Status |',
    '|---|---|---|---|---|---|---|',
    ...payload.items.map((item) => `| ${item.id} | ${item.method} | ${item.route} | ${item.effect} | ${item.domain} | ${item.risk} | ${item.status} |`),
    '',
  ].join('\n');
}

const payload = build();
const json = `${JSON.stringify(payload, null, 2)}\n`;
const report = markdown(payload);
if (checkOnly) {
  if (!existsSync(outputPath) || !existsSync(markdownPath) || readFileSync(outputPath, 'utf8') !== json || readFileSync(markdownPath, 'utf8') !== report) {
    throw new Error('AdminAI capability baseline is stale. Run: node scripts/generate-admin-ai-capability-baseline.mjs');
  }
} else {
  writeFileSync(outputPath, json);
  writeFileSync(markdownPath, report);
}
process.stdout.write(`AdminAI capability baseline is current (${payload.items.length} items; activation=${payload.activation}).\n`);
