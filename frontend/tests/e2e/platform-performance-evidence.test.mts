import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import {
  readPerformanceSourceBinding,
  resolveRawEvidenceOutput,
  writeJsonEvidenceCreateNew,
} from '../../scripts/performance-evidence-io.mjs';
import {
  aggregateEligibleReads,
  eligibleReadIdentity,
  nearestRankP75,
} from './platform-performance-evidence.ts';

test('eligible reads preserve exact identity except for the transient _rsc value', () => {
  const allowedOrigins = {
    appOrigin: 'https://app.example.test',
    apiOrigin: 'https://api.example.test',
  };
  const sensitiveUrl =
    'https://api.example.test/api/students/123?phone=01000000000&scope=current&_rsc=first';
  const first = eligibleReadIdentity({
    method: 'GET',
    resourceType: 'fetch',
    url: sensitiveUrl,
  }, allowedOrigins);
  const second = eligibleReadIdentity({
    method: 'GET',
    resourceType: 'fetch',
    url: sensitiveUrl.replace('first', 'second'),
  }, allowedOrigins);
  const reorderedQuery = eligibleReadIdentity({
    method: 'GET',
    resourceType: 'xhr',
    url: 'https://api.example.test/api/students/123?_rsc=third&scope=current&phone=01000000000',
  }, allowedOrigins);
  const differentQuery = eligibleReadIdentity({
    method: 'GET',
    resourceType: 'fetch',
    url: sensitiveUrl.replace('01000000000', '01000000001'),
  }, allowedOrigins);

  assert.deepEqual(first, second);
  assert.deepEqual(first, reorderedQuery);
  assert.notDeepEqual(first, differentQuery);
  assert.equal(JSON.stringify(first).includes('01000000000'), false);
  assert.match(first?.identitySha256 ?? '', /^[0-9a-f]{64}$/);

  for (const request of [
    { method: 'POST', resourceType: 'fetch' },
    { method: 'GET', resourceType: 'document' },
    { method: 'GET', resourceType: 'image' },
  ]) {
    assert.equal(
      eligibleReadIdentity({ ...request, url: sensitiveUrl }, allowedOrigins),
      null,
    );
  }
  assert.equal(
    eligibleReadIdentity(
      { method: 'GET', resourceType: 'fetch', url: 'https://external.example.test/data' },
      allowedOrigins,
    ),
    null,
  );
  assert.equal(
    eligibleReadIdentity(
      { method: 'GET', resourceType: 'xhr', url: 'https://api.example.test/api/v1/metrics/web-vitals' },
      allowedOrigins,
    ),
    null,
  );
});

test('eligible read aggregation retains per-identity counts for duplicate recomputation', () => {
  const left = { identitySha256: 'a'.repeat(64), category: 'api-read' as const };
  const right = { identitySha256: 'b'.repeat(64), category: 'rsc-read' as const };
  assert.deepEqual(aggregateEligibleReads([left, left, right, right, right]), [
    { ...left, count: 2 },
    { ...right, count: 3 },
  ]);
});

test('nearest-rank p75 uses the fifteenth value from exactly twenty samples', () => {
  assert.equal(nearestRankP75(Array.from({ length: 20 }, (_, index) => index + 1)), 15);
  assert.throws(() => nearestRankP75([1, 2, 3]), /requires 20/);
  assert.throws(
    () => nearestRankP75([...Array.from({ length: 19 }, () => 1), Number.NaN]),
    /finite non-negative/,
  );
});

test('raw evidence output is repository-bound, source-bound, atomic, and create-new', () => {
  const projectRoot = fs.mkdtempSync(
    path.join(fs.realpathSync(os.tmpdir()), 'massar-performance-'),
  );
  try {
    const rawRoot = path.join(projectRoot, 'artifacts/performance-167/final/raw');
    fs.mkdirSync(rawRoot, { recursive: true });
    const sourceManifestPath = path.join(rawRoot, 'source-manifest.json');
    const sourceManifest = {
      schemaVersion: 2,
      releaseId: `src-${'b'.repeat(12)}`,
      gitCommit: 'a'.repeat(40),
      sourceStateSha256: 'b'.repeat(64),
      dirtySourceSnapshot: true,
      sourceDigestAlgorithm: 'massar-release-snapshot-sha256-v2',
      sourcePaths: ['frontend'],
    };
    const sourceBytes = `${JSON.stringify(sourceManifest)}\n`;
    fs.writeFileSync(sourceManifestPath, sourceBytes);

    const source = readPerformanceSourceBinding({
      repositoryRoot: projectRoot,
      manifestPath: 'artifacts/performance-167/final/raw/source-manifest.json',
    });
    assert.deepEqual(source, {
      releaseId: sourceManifest.releaseId,
      gitCommit: sourceManifest.gitCommit,
      sourceStateSha256: sourceManifest.sourceStateSha256,
      dirtySourceSnapshot: true,
      sourceDigestAlgorithm: sourceManifest.sourceDigestAlgorithm,
      manifestSha256: createHash('sha256').update(sourceBytes).digest('hex'),
    });

    const outputPath = resolveRawEvidenceOutput(
      projectRoot,
      'artifacts/performance-167/final/raw/browser-samples.json',
      'unused.json',
    );
    writeJsonEvidenceCreateNew(outputPath, { schemaVersion: 1, source });
    assert.equal(fs.lstatSync(outputPath).isFile(), true);
    assert.equal(fs.lstatSync(outputPath).isSymbolicLink(), false);
    assert.throws(
      () => writeJsonEvidenceCreateNew(outputPath, { schemaVersion: 2 }),
      /Refusing to overwrite/,
    );
    assert.throws(
      () =>
        resolveRawEvidenceOutput(
          projectRoot,
          'artifacts/performance-167/final/browser-samples.json',
          'unused.json',
        ),
      /must stay under/,
    );

    const symlinkPath = path.join(rawRoot, 'symlink.json');
    fs.symlinkSync(outputPath, symlinkPath);
    assert.throws(
      () => writeJsonEvidenceCreateNew(symlinkPath, { schemaVersion: 1 }),
      /Refusing symlink/,
    );
  } finally {
    fs.rmSync(projectRoot, { recursive: true, force: true });
  }
});

test('raw evidence writer refuses a symlink anywhere in its directory chain', () => {
  const projectRoot = fs.mkdtempSync(
    path.join(fs.realpathSync(os.tmpdir()), 'massar-performance-link-'),
  );
  try {
    const finalRoot = path.join(projectRoot, 'artifacts/performance-167/final');
    const redirectedRoot = path.join(projectRoot, 'redirected-raw');
    fs.mkdirSync(finalRoot, { recursive: true });
    fs.mkdirSync(redirectedRoot);
    fs.symlinkSync(redirectedRoot, path.join(finalRoot, 'raw'));

    assert.throws(
      () =>
        writeJsonEvidenceCreateNew(
          path.join(finalRoot, 'raw/browser-samples.json'),
          { schemaVersion: 1 },
        ),
      /Refusing symlink evidence directory/,
    );
    assert.equal(fs.existsSync(path.join(redirectedRoot, 'browser-samples.json')), false);
  } finally {
    fs.rmSync(projectRoot, { recursive: true, force: true });
  }
});
