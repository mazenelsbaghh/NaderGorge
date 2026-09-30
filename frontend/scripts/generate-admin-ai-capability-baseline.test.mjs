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

test('AdminAI graph excludes participant support methods unused by Admin modules', () => {
  const graph = collectAdminCallGraph();
  const supportCalls = graph.calls.filter((call) =>
    call.source.file === 'frontend/src/services/live-support-service.ts');

  assert.ok(supportCalls.length > 0);
  assert.ok(supportCalls.every((call) => !call.path.startsWith('/live-support/participant/')));
});

test('AdminAI graph excludes unused self-service mutations from imported service objects', () => {
  const graph = collectAdminCallGraph();
  const routes = graph.calls.map((call) => `${call.method} ${call.path}`);

  assert.ok(!routes.includes('POST /codes/activate'));
  assert.ok(!routes.includes('POST /hr/payroll/self/financial-requests'));
  assert.ok(!routes.includes('POST /v1/assistant/tasks/{taskId}/resolve'));
});

test('AdminAI graph keeps shared subscriber methods without unrelated teacher actions', () => {
  const graph = collectAdminCallGraph();
  const teacherCalls = graph.calls.filter((call) =>
    call.source.file === 'frontend/src/services/teacher-service.ts');
  const routes = teacherCalls.map((call) => `${call.method} ${call.path}`);

  assert.ok(routes.includes('GET /teacher/content/{contentType}/{id}/subscribers'));
  assert.ok(routes.includes('GET /teacher/content/{contentType}/{id}/subscribers/export'));
  assert.ok(!routes.includes('POST /teacher/essays/{id}/grade'));
  assert.ok(!routes.includes('POST /teacher/profile/upload-image'));
});

test('AdminAI graph excludes service methods referenced only by TypeScript types', () => {
  const target = fileURLToPath(new URL('../src/services/student-service.ts', import.meta.url));
  const importer = fileURLToPath(new URL('../src/components/admin/QuestionEditor.tsx', import.meta.url));
  const source = ts.createSourceFile(importer, `
    import { studentService } from '@/services/student-service';
    type Result = ReturnType<typeof studentService.getProfile>;
    void studentService.uploadAudio('sample');
  `, ts.ScriptTarget.Latest, true);

  assert.deepEqual([...provenServiceMembers(target, 'studentService', new Map([[importer, source]]))],
    ['uploadAudio']);
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
