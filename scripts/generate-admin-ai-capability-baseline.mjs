#!/usr/bin/env node
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const root = process.cwd();
const endpointPath = resolve(root, 'tests/endpoint_inventory.json');
const runtimePath = resolve(root, 'tests/admin_ai_runtime_endpoint_inventory.json');
const frontendPath = resolve(root, 'tests/admin_ai_frontend_reachable_calls.json');
const outputPath = resolve(root, 'tests/admin_ai_capability_baseline.json');
const markdownPath = resolve(root, 'tests/admin_ai_capability_baseline.md');
const checkOnly = process.argv.includes('--check');

const strongTerms = /(delete|remove|revoke|reset|password|role|permission|disable|toggle|bulk|finance|payment|wallet|refund|settlement|treasury|expense|salary|payroll|publish|cancel|migrat|transfer|generate)/i;
const externalTerms = /(whatsapp|bunny|upload|export|download|sync|analy[sz]e)/i;
const directControllerFamilies = /^(AdminFinance|AdminPlatformFinance|AdminTeacherFinanceCenter|AdminTeacherCodeFinance|AdminSharedPackages|HrApprovals|HrDocumentsAssets|HrLeave|HrPayroll|HrPerformanceCases|HrRecruitmentLifecycle|HrShifts)$/;
const directControllerOperations = new Set(['AdminController.GetPendingEssays']);

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

function domainFor(value) {
  const input = value.toLowerCase();
  if (/(finance|wallet|recharge|payment|treasury|refund|expense|settlement|accounting)/.test(input)) return 'finance';
  if (/(hr|employee|payroll|leave|recruit|attendance|shift|governance)/.test(input)) return 'hr';
  if (/(student|user|role|auth|device|profile)/.test(input)) return 'identity';
  if (/(content|lesson|video|package|subject|teacher|code|exam|question|homework)/.test(input)) return 'content';
  if (/(gift|sale|coupon|purchase|commercial)/.test(input)) return 'commercial';
  if (/(support|chat|crm|operation)/.test(input)) return 'support';
  if (/(report|log|media|audit)/.test(input)) return 'reporting';
  return 'other';
}

function semantics(method, descriptor) {
  const external = externalTerms.test(descriptor);
  const mutation = method !== 'GET' && method !== 'ANY';
  const effect = mutation ? (external ? 'external-side-effect' : 'mutation') :
    (/export|download/i.test(descriptor) ? 'export' : /preview/i.test(descriptor) ? 'preview' : 'read');
  const risk = mutation && strongTerms.test(descriptor) ? 'strong' : mutation ? 'ordinary' : 'none';
  return {
    effect,
    risk,
    confirmation: risk === 'strong' ? 'strong' : risk === 'ordinary' ? 'ordinary' : 'none',
    status: mutation ? 'blocked' : 'candidate',
    blocker: mutation ? 'Requires a reviewed capability adapter that calls an authoritative application command/service.' : undefined,
  };
}

function includeEndpoint(endpoint) {
  return endpoint.controller.startsWith('Admin') ||
    endpoint.controller.startsWith('Hr') ||
    ['CrmController', 'InternalChatController', 'LiveSupportAdminController', 'LiveSupportAIAdminController', 'WhatsAppController'].includes(endpoint.controller) ||
    endpoint.path.startsWith('/api/hr/');
}

function createItem(kind, method, route, source, descriptor, authoritativeOperation) {
  const semantic = semantics(method, descriptor);
  const mutation = semantic.risk !== 'none';
  const controllerName = descriptor.split('.')[0]?.replace(/Controller$/, '') ?? '';
  const directControllerWrite = mutation && (directControllerFamilies.test(controllerName) || directControllerOperations.has(descriptor));
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
    ...(semantic.blocker ? { blocker: directControllerWrite
      ? 'Direct controller database write must be extracted into an authoritative application command/service before adaptation.'
      : semantic.blocker } : {}),
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
  const items = [
    ...endpoints.map((endpoint) => createItem(
      'backend-endpoint', endpoint.method, endpoint.path, endpoint.source,
      `${endpoint.controller}.${endpoint.action}`,
      `diagnostic:${endpoint.controller}.${endpoint.action}`,
    )),
    ...frontend.calls.map((call) => createItem(
      'frontend-call', call.method, call.path, call.source,
      call.source.file,
      'unresolved:frontend-contract',
    )),
  ].sort((left, right) => left.id.localeCompare(right.id));
  const payload = {
    schemaVersion: 1,
    generatedAtUtc: '2026-08-11T00:00:00.000Z',
    activation: 'blocked',
    sources: {
      runtime: { path: 'tests/admin_ai_runtime_endpoint_inventory.json', digest: digest(runtimeRaw) },
      frontend: { path: 'tests/admin_ai_frontend_reachable_calls.json', digest: digest(frontendRaw) },
      semantic: { path: 'scripts/generate-admin-ai-capability-baseline.mjs', digest: digest('heuristic-v1-reviewed-required') },
    },
    items,
    exclusions: [],
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
