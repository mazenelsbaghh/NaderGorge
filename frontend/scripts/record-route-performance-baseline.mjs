import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import vm from 'node:vm';
import zlib from 'node:zlib';
import { pathToFileURL } from 'node:url';

import {
  readPerformanceSourceBinding,
  resolveRawEvidenceOutput,
  writeJsonEvidenceCreateNew,
} from './performance-evidence-io.mjs';

const frontendRoot = path.resolve(import.meta.dirname, '..');
const repositoryRoot = path.resolve(frontendRoot, '..');
const nextRoot = path.join(frontendRoot, '.next');

const routes = [
  {
    name: 'login',
    pathname: '/login',
    manifestKey: '/(public)/login/page',
    manifestPath: '.next/server/app/(public)/login/page_client-reference-manifest.js',
  },
  {
    name: 'register',
    pathname: '/register',
    manifestKey: '/(public)/register/page',
    manifestPath: '.next/server/app/(public)/register/page_client-reference-manifest.js',
  },
  {
    name: 'student',
    pathname: '/student',
    manifestKey: '/student/page',
    manifestPath: '.next/server/app/student/page_client-reference-manifest.js',
  },
  {
    name: 'admin',
    pathname: '/admin',
    manifestKey: '/admin/page',
    manifestPath: '.next/server/app/admin/page_client-reference-manifest.js',
  },
];

function runProductionBuild() {
  const startedAt = Date.now();
  console.log('Creating a fresh production build before recording route resources...');
  execFileSync('npm', ['run', 'build'], {
    cwd: frontendRoot,
    env: {
      ...process.env,
      NEXT_TELEMETRY_DISABLED: '1',
      NODE_ENV: 'production',
    },
    stdio: 'inherit',
  });
  return startedAt;
}

function requireFreshFile(filePath, buildStartedAt) {
  if (!fs.existsSync(filePath)) {
    throw new Error(`Required production-build output is missing: ${path.relative(frontendRoot, filePath)}`);
  }

  const { mtimeMs } = fs.statSync(filePath);
  // Filesystems may expose timestamps at one-second precision.
  if (mtimeMs < buildStartedAt - 1_000) {
    throw new Error(
      `Refusing stale production-build output: ${path.relative(frontendRoot, filePath)}`
    );
  }
}

function readRouteManifest(route, buildStartedAt) {
  const absolutePath = path.join(frontendRoot, route.manifestPath);
  requireFreshFile(absolutePath, buildStartedAt);

  const sandbox = {};
  sandbox.globalThis = sandbox;
  vm.runInNewContext(fs.readFileSync(absolutePath, 'utf8'), sandbox, {
    filename: absolutePath,
    timeout: 5_000,
  });

  const manifest = sandbox.__RSC_MANIFEST?.[route.manifestKey];
  if (!manifest) {
    throw new Error(`Route manifest key not found: ${route.manifestKey}`);
  }
  return manifest;
}

function normalizeResourcePath(resourcePath) {
  return resourcePath
    .replace(/^\/_next\//, '')
    .replace(/^_next\//, '')
    .replace(/^\/+/, '');
}

function addResources(target, resources) {
  for (const resource of resources ?? []) {
    const normalized = normalizeResourcePath(
      typeof resource === 'string' ? resource : resource.path
    );
    if (normalized.endsWith('.js') || normalized.endsWith('.css')) {
      target.add(normalized);
    }
  }
}

function getRouteResourceSets(manifest) {
  const initial = new Set();
  const all = new Set();

  for (const resources of Object.values(manifest.entryJSFiles ?? {})) {
    addResources(initial, resources);
  }
  for (const resources of Object.values(manifest.entryCSSFiles ?? {})) {
    addResources(initial, resources);
  }
  for (const moduleEntry of Object.values(manifest.clientModules ?? {})) {
    addResources(all, moduleEntry.chunks);
  }
  addResources(all, initial);

  return {
    initial,
    all,
    deferred: new Set([...all].filter((resource) => !initial.has(resource))),
  };
}

function intersect(sets) {
  if (sets.length === 0) return new Set();
  return new Set([...sets[0]].filter((value) => sets.every((set) => set.has(value))));
}

function resourceEvidence(resourcePath) {
  const absolutePath = path.join(nextRoot, resourcePath);
  if (!fs.existsSync(absolutePath)) {
    throw new Error(`Manifest resource does not exist: ${resourcePath}`);
  }

  const source = fs.readFileSync(absolutePath);
  return {
    path: resourcePath,
    type: path.extname(resourcePath).slice(1),
    bytes: source.byteLength,
    gzipBytes: zlib.gzipSync(source, { level: 9 }).byteLength,
    brotliBytes: zlib.brotliCompressSync(source, {
      params: {
        [zlib.constants.BROTLI_PARAM_QUALITY]: 11,
      },
    }).byteLength,
  };
}

function summarize(resourcePaths) {
  const resources = [...resourcePaths].sort().map(resourceEvidence);
  return {
    resourceCount: resources.length,
    bytes: resources.reduce((total, resource) => total + resource.bytes, 0),
    gzipBytes: resources.reduce((total, resource) => total + resource.gzipBytes, 0),
    brotliBytes: resources.reduce((total, resource) => total + resource.brotliBytes, 0),
    resources,
  };
}

function cliValues(args = process.argv.slice(2)) {
  const { values } = parseArgs({
    args,
    options: {
      output: { type: 'string' },
      manifest: { type: 'string' },
    },
  });
  return values;
}

export function reportOutputPath(args = process.argv.slice(2)) {
  const values = cliValues(args);
  return resolveRawEvidenceOutput(
    repositoryRoot,
    values.output,
    'route-resources.json',
  );
}

function readSource(args = process.argv.slice(2)) {
  const values = cliValues(args);
  return readPerformanceSourceBinding({
    repositoryRoot,
    manifestPath: values.manifest ?? process.env.PERFORMANCE_SOURCE_MANIFEST,
  });
}

function main() {
  const cliArgs = process.argv.slice(2);
  const outputPath = reportOutputPath(cliArgs);
  const source = readSource(cliArgs);
  const buildStartedAt = runProductionBuild();
  const buildIdPath = path.join(nextRoot, 'BUILD_ID');
  requireFreshFile(buildIdPath, buildStartedAt);

  const routeSets = routes.map((route) => ({
    ...route,
    resources: getRouteResourceSets(readRouteManifest(route, buildStartedAt)),
  }));
  const sharedInitial = intersect(routeSets.map((route) => route.resources.initial));

  const evidence = {
    schemaVersion: 1,
    evidenceType: 'route-resource-measurement',
    generatedAt: new Date().toISOString(),
    source,
    platform: {
      operatingSystem: process.platform,
      architecture: process.arch,
      nodeVersion: process.version,
    },
    measurement: {
      source: 'fresh Next.js production build client reference manifests',
      productionBuildExecuted: true,
      buildStartedAt: new Date(buildStartedAt).toISOString(),
      buildId: fs.readFileSync(buildIdPath, 'utf8').trim(),
      compression: {
        gzipLevel: 9,
        brotliQuality: 11,
        note: 'Deterministic local compression for before/after comparison; effective transfer is verified separately.',
      },
      classification: {
        shared:
          'Initial JS/CSS resources present in all four measured routes.',
        initial:
          'Route initial JS/CSS excluding the resources classified as shared.',
        deferred:
          'Client-manifest JS resources not present in the route initial entry sets.',
      },
    },
    shared: summarize(sharedInitial),
    routes: Object.fromEntries(
      routeSets.map((route) => [
        route.name,
        {
          pathname: route.pathname,
          manifestKey: route.manifestKey,
          initial: summarize(
            new Set(
              [...route.resources.initial].filter(
                (resource) => !sharedInitial.has(resource)
              )
            )
          ),
          shared: summarize(sharedInitial),
          deferred: summarize(route.resources.deferred),
          total: summarize(route.resources.all),
        },
      ])
    ),
  };

  writeJsonEvidenceCreateNew(outputPath, evidence);
  console.log(`Route baseline written to ${path.relative(repositoryRoot, outputPath)}`);
}

if (
  process.argv[1] &&
  import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href
) {
  main();
}
