import assert from 'node:assert/strict';
import test from 'node:test';
import { collectAdminCallGraph } from './generate-admin-ai-capability-baseline.mjs';

test('AdminAI reachable call graph is deterministic and rooted in Admin routes', () => {
  const first = collectAdminCallGraph();
  const second = collectAdminCallGraph();

  assert.deepEqual(first, second);
  assert.ok(first.reachableFileCount > 0);
  assert.ok(first.reachableFiles.some((file) => file.route === '/admin'));
  assert.ok(first.reachableFiles.every((file) => file.file.startsWith('frontend/src/')));
});

test('AdminAI reachable call graph retains dynamic calls explicitly', () => {
  const graph = collectAdminCallGraph();

  assert.ok(graph.calls.every((call) => call.path.startsWith('/') || call.path === '<dynamic>'));
  assert.ok(graph.calls.every((call) => typeof call.dynamic === 'boolean'));
  assert.ok(graph.calls.every((call) => call.source.file.startsWith('frontend/src/')));
  assert.ok(graph.unreachableCalls.every((call) => !graph.calls.some((reachable) =>
    reachable.source.file === call.source.file && reachable.source.line === call.source.line,
  )));
});
