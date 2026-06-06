#!/usr/bin/env node

import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { extname, join, resolve } from 'node:path';

const args = new Set(process.argv.slice(2));
const staticOnly = args.has('--static-only');

const requiredServices = ['landing', 'student', 'admin', 'backend', 'worker', 'db', 'redis'];
const frontendServices = ['landing', 'student', 'admin'];
const appServices = ['landing', 'student', 'admin', 'backend'];
const healthcheckServices = ['landing', 'student', 'admin', 'backend', 'worker', 'db', 'redis'];
const checkedBrandFiles = [
  'frontend/src/app/layout.tsx',
  'frontend/src/app/(public)/login/page.tsx',
  'frontend/src/app/api/video/embed/route.ts',
  'frontend/public/images/logo.svg',
  'frontend/public/images/logo-mark.svg',
  '.env.example',
  'Makefile',
  'docker-compose.yml',
  'PRODUCT.md',
];
const brandScanRoots = ['frontend/src', 'frontend/public'];
const textFileExtensions = new Set([
  '.css',
  '.html',
  '.js',
  '.jsx',
  '.json',
  '.md',
  '.mjs',
  '.svg',
  '.ts',
  '.tsx',
  '.txt',
]);

const composeEnv = {
  ...process.env,
  JWT_SECRET: process.env.JWT_SECRET || 'verify_surface_secret_12345678901234567890',
  API_CALLBACK_SECRET: process.env.API_CALLBACK_SECRET || 'verify_api_callback_secret',
  AI_CALLBACK_SECRET: process.env.AI_CALLBACK_SECRET || 'verify_ai_callback_secret',
  PARENT_REPORT_SIGNING_SECRET:
    process.env.PARENT_REPORT_SIGNING_SECRET || 'verify_parent_report_secret',
  WORKER_ADMIN_TOKEN: process.env.WORKER_ADMIN_TOKEN || 'verify_worker_admin_token',
};

function fail(message) {
  throw new Error(message);
}

function readComposeConfig() {
  const output = execFileSync('docker', ['compose', 'config', '--format', 'json'], {
    cwd: process.cwd(),
    env: composeEnv,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
  });

  return JSON.parse(output);
}

function getEnvironment(service) {
  if (!service.environment) return {};
  if (Array.isArray(service.environment)) {
    return Object.fromEntries(
      service.environment.map((entry) => {
        const index = entry.indexOf('=');
        if (index === -1) return [entry, ''];
        return [entry.slice(0, index), entry.slice(index + 1)];
      }),
    );
  }

  return service.environment;
}

function getPublishedPorts(service) {
  return (service.ports || [])
    .map((port) => String(port.published || port.host_port || ''))
    .filter(Boolean);
}

function assertServices(config) {
  for (const name of requiredServices) {
    if (!config.services?.[name]) {
      fail(`Missing required service: ${name}`);
    }
  }
}

function assertMasarNaming(config) {
  for (const name of [...requiredServices, 'migrator']) {
    const service = config.services?.[name];
    if (!service) continue;
    const containerName = service.container_name || '';
    if (!containerName.startsWith('masar_')) {
      fail(`Service ${name} container_name must start with masar_: ${containerName}`);
    }
    if (/nadergorge/i.test(containerName)) {
      fail(`Service ${name} container_name still contains old brand: ${containerName}`);
    }
  }
}

function assertUniqueAppPorts(config) {
  const seen = new Map();
  for (const name of appServices) {
    const ports = getPublishedPorts(config.services[name]);
    if (ports.length === 0) {
      fail(`Service ${name} must publish a host port`);
    }

    for (const port of ports) {
      const owner = seen.get(port);
      if (owner) {
        fail(`Duplicate app host port ${port} used by ${owner} and ${name}`);
      }
      seen.set(port, name);
    }
  }
}

function assertHealthchecks(config) {
  for (const name of healthcheckServices) {
    const service = config.services[name];
    if (!service.healthcheck?.test) {
      fail(`Service ${name} must declare a healthcheck`);
    }
  }
}

function assertFrontendEnvironment(config) {
  const requiredEnv = [
    'APP_SURFACE',
    'NEXT_PUBLIC_API_URL',
    'NEXT_PUBLIC_BACKEND_URL',
    'INTERNAL_API_URL',
    'INTERNAL_BACKEND_URL',
  ];

  for (const name of frontendServices) {
    const env = getEnvironment(config.services[name]);
    for (const key of requiredEnv) {
      if (!env[key]) {
        fail(`Frontend service ${name} missing environment variable ${key}`);
      }
    }
    if (env.APP_SURFACE !== name) {
      fail(`Frontend service ${name} has wrong APP_SURFACE: ${env.APP_SURFACE}`);
    }
    if (String(env.NEXT_PUBLIC_API_URL).includes('backend:5245')) {
      fail(`Frontend service ${name} exposes Docker-only backend DNS to browsers`);
    }
    if (!String(env.INTERNAL_API_URL).includes('backend:5245')) {
      fail(`Frontend service ${name} INTERNAL_API_URL must use Docker backend DNS`);
    }
  }
}

function assertCors(config) {
  const env = getEnvironment(config.services.backend);
  const cors = String(env.Cors__AllowedOrigins || '');
  for (const origin of ['http://localhost:8738', 'http://localhost:8739', 'http://localhost:8740']) {
    if (!cors.includes(origin)) {
      fail(`Backend CORS defaults missing ${origin}`);
    }
  }
}

function assertBrandStrings() {
  const forbidden = [/مسار أكاديمي/, /Massar Academy/, /MASSAR ACADEMY/, /Nader George/, /نادر جورج/];
  const files = new Set(checkedBrandFiles);

  for (const root of brandScanRoots) {
    const absoluteRoot = resolve(process.cwd(), root);
    const stack = [absoluteRoot];
    while (stack.length > 0) {
      const current = stack.pop();
      for (const entry of readdirSync(current, { withFileTypes: true })) {
        const absolutePath = join(current, entry.name);
        if (entry.isDirectory()) {
          if (!['.next', 'node_modules'].includes(entry.name)) stack.push(absolutePath);
          continue;
        }

        if (entry.isFile() && textFileExtensions.has(extname(entry.name))) {
          files.add(absolutePath);
        }
      }
    }
  }

  for (const file of files) {
    const path = resolve(process.cwd(), file);
    if (!statSync(path).isFile()) continue;
    const content = readFileSync(path, 'utf8');
    for (const pattern of forbidden) {
      if (pattern.test(content)) {
        fail(`Old visible brand string ${pattern} found in ${file}`);
      }
    }
  }
}

async function fetchManual(url) {
  return fetch(url, { redirect: 'manual' });
}

async function assertRuntimeHttp() {
  const origins = {
    landing: process.env.LANDING_PUBLIC_ORIGIN || 'http://localhost:8738',
    student: process.env.STUDENT_PUBLIC_ORIGIN || 'http://localhost:8739',
    admin: process.env.ADMIN_PUBLIC_ORIGIN || 'http://localhost:8740',
    backend: process.env.NEXT_PUBLIC_BACKEND_URL || 'http://localhost:5245',
  };

  const checks = [
    ['landing', `${origins.landing}/`],
    ['student', `${origins.student}/`],
    ['admin', `${origins.admin}/`],
    ['backend', `${origins.backend}/api/health`],
  ];

  for (const [name, url] of checks) {
    const response = await fetch(url);
    if (!response.ok) {
      fail(`${name} runtime check failed for ${url}: ${response.status}`);
    }
  }

  const landingStudent = await fetchManual(`${origins.landing}/student`);
  if (![301, 302, 307, 308].includes(landingStudent.status)) {
    fail(`landing /student must redirect, got ${landingStudent.status}`);
  }
  const landingStudentLocation = landingStudent.headers.get('location') || '';
  if (!landingStudentLocation.startsWith(origins.student)) {
    fail(`landing /student redirects to wrong origin: ${landingStudentLocation}`);
  }

  const landingAdmin = await fetchManual(`${origins.landing}/admin`);
  if (![301, 302, 307, 308].includes(landingAdmin.status)) {
    fail(`landing /admin must redirect, got ${landingAdmin.status}`);
  }
  const landingAdminLocation = landingAdmin.headers.get('location') || '';
  if (!landingAdminLocation.startsWith(origins.admin)) {
    fail(`landing /admin redirects to wrong origin: ${landingAdminLocation}`);
  }
}

async function main() {
  const config = readComposeConfig();
  assertServices(config);
  assertMasarNaming(config);
  assertUniqueAppPorts(config);
  assertHealthchecks(config);
  assertFrontendEnvironment(config);
  assertCors(config);
  assertBrandStrings();

  if (!staticOnly) {
    await assertRuntimeHttp();
  }

  console.log(staticOnly ? 'Surface static verification passed.' : 'Surface static and runtime verification passed.');
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
