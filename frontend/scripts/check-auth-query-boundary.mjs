#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';

const source = fs.readFileSync(
  path.resolve(import.meta.dirname, '../src/stores/auth-store.ts'),
  'utf8',
);

const contracts = [
  {
    pattern:
      /setAuth:\s*\([^)]*\)\s*=>\s*\{[\s\S]*?clearQueriesForBoundaryTransition\(get\(\)\.user,\s*user\)/,
    message: 'setAuth must clear cached protected data before an identity/role transition',
  },
  {
    pattern:
      /clearAuth:\s*\(\)\s*=>\s*\{[\s\S]*?platformQueryClient\.removeQueries\(\)/,
    message: 'clearAuth must synchronously remove protected query state',
  },
  {
    pattern:
      /clearQueriesForBoundaryTransition\(latest\.user,\s*snapshot\.user as User\)/,
    message: 'session refresh must clear data before a changed authorization boundary renders',
  },
  {
    pattern:
      /clearQueriesForBoundaryTransition\(get\(\)\.user,\s*storedAuth\.user as User\)/,
    message: 'storage bootstrap must clear data before restoring another identity',
  },
  {
    pattern:
      /clearQueriesForBoundaryTransition\(get\(\)\.user,\s*payload\.user\)/,
    message: 'cookie refresh must clear data before restoring another identity',
  },
];

const failures = contracts
  .filter(({ pattern }) => !pattern.test(source))
  .map(({ message }) => message);

if (failures.length > 0) {
  console.error('Auth/query boundary contract failed:');
  failures.forEach((failure) => console.error(`- ${failure}`));
  process.exit(1);
}

console.log('auth/query boundary contracts passed');
