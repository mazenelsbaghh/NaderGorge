import { spawnSync } from 'node:child_process';
import path from 'node:path';

const testPath = path.resolve(import.meta.dirname, '../src/lib/live-support-client-contract.test.mts');
const result = spawnSync(process.execPath, ['--experimental-strip-types', testPath], { stdio: 'inherit' });
if (result.error) throw result.error;
process.exit(result.status ?? 1);
