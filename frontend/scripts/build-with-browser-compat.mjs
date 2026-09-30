import { randomUUID } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { writeCompatibleStylesheet } from './browser-compatible-css.mjs';

const buildId = randomUUID();
const build = spawnSync(process.execPath, ['node_modules/next/dist/bin/next', 'build', ...process.argv.slice(2)], {
  stdio: 'inherit',
  env: { ...process.env, NEXT_PUBLIC_BROWSER_COMPAT_ID: buildId },
});
if (build.error) throw build.error;
if (build.status !== 0) process.exit(build.status ?? 1);
await writeCompatibleStylesheet('.next', buildId);
