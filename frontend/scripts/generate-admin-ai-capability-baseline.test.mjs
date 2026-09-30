import assert from 'node:assert/strict';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import ts from '../node_modules/typescript/lib/typescript.js';
import { collectAdminCallGraph, provenServiceMembers } from './generate-admin-ai-capability-baseline.mjs';

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

test('AdminAI graph includes only student service methods invoked from Admin modules', () => {
  const graph = collectAdminCallGraph();
  const studentCalls = graph.calls.filter((call) =>
    call.source.file === 'frontend/src/services/student-service.ts');

  assert.deepEqual(studentCalls.map((call) => `${call.method} ${call.path}`), [
    'POST /student/upload-audio',
  ]);
});

test('AdminAI graph excludes unused student purchase and lesson comment service methods', () => {
  const graph = collectAdminCallGraph();
  const sharedPackageCalls = graph.calls.filter((call) =>
    call.source.file === 'frontend/src/services/shared-package-service.ts');
  const contentCalls = graph.calls.filter((call) =>
    call.source.file === 'frontend/src/services/content-service.ts');

  assert.ok(sharedPackageCalls.length > 0);
  assert.ok(sharedPackageCalls.every((call) => call.path.startsWith('/admin/shared-packages')));
  assert.ok(contentCalls.length > 0);
  assert.ok(contentCalls.every((call) => !call.path.includes('/comments')));
});

test('AdminAI graph keeps all service methods when the imported object escapes', () => {
  const target = fileURLToPath(new URL('../src/services/student-service.ts', import.meta.url));
  const importer = fileURLToPath(new URL('../src/components/admin/QuestionEditor.tsx', import.meta.url));
  const source = ts.createSourceFile(importer, `
    import { studentService } from '@/services/student-service';
    register(studentService);
  `, ts.ScriptTarget.Latest, true);

  assert.equal(provenServiceMembers(target, 'studentService', new Map([[importer, source]])), null);
});
