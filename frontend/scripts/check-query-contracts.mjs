import { readdirSync, readFileSync } from 'node:fs';

const servicesDir = new URL('../src/services/', import.meta.url);
const contractSource = readFileSync(new URL('../src/lib/query-contracts.ts', import.meta.url), 'utf8');
const mutationPattern = /apiClient\.(?:post|put|patch|delete)\b/g;
const serviceFiles = readdirSync(servicesDir).filter((file) => file.endsWith('.ts'));
const discovered = new Map();

for (const file of serviceFiles) {
  const source = readFileSync(new URL(file, servicesDir), 'utf8');
  const matches = [...source.matchAll(mutationPattern)];
  if (matches.length > 0) {
    discovered.set(file, { count: matches.length, lines: matches.map((match) => source.slice(0, match.index).split('\n').length) });
  }
}

if (discovered.size === 0) throw new Error('No service mutations were discovered; inventory scan is invalid.');

const records = new Map();
const recordPattern = /\['([^']+\.ts)',\s*(\d+),\s*'([^']+)',\s*\[([^\]]*)\]\]/g;
for (const match of contractSource.matchAll(recordPattern)) {
  records.set(match[1], { count: Number(match[2]), domain: match[3], keys: match[4].match(/'[^']+'/g) ?? [] });
}

const missing = [...discovered.keys()].filter((file) => !records.has(file));
const unexpected = [...records.keys()].filter((file) => !discovered.has(file));
const countMismatches = [...discovered.entries()]
  .filter(([file, result]) => records.get(file)?.count !== result.count)
  .map(([file, result]) => `${file}: source=${result.count}, contract=${records.get(file)?.count ?? 'missing'}`);

if (missing.length || unexpected.length || countMismatches.length) {
  const details = [
    missing.length ? `missing contracts: ${missing.join(', ')}` : '',
    unexpected.length ? `stale contracts: ${unexpected.join(', ')}` : '',
    countMismatches.length ? `count mismatches: ${countMismatches.join('; ')}` : '',
  ].filter(Boolean).join('\n');
  throw new Error(`Query mutation contract coverage failed.\n${details}`);
}

if (!contractSource.includes('mutationContractRecords') || !contractSource.includes('validateQueryContracts')) {
  throw new Error('Typed mutation contract registry is missing required exports.');
}

const duplicateKeys = [...records.entries()].filter(([, record]) => new Set(record.keys).size !== record.keys.length);
if (duplicateKeys.length) throw new Error(`Duplicate keys in mutation contracts: ${duplicateKeys.map(([file]) => file).join(', ')}`);

const legacyForceFiles = serviceFiles.filter((file) => /force\s*:\s*true/.test(readFileSync(new URL(file, servicesDir), 'utf8')));
if (legacyForceFiles.length > 0) throw new Error(`Unclassified force refresh remains in: ${legacyForceFiles.join(', ')}`);

const totalMutations = [...discovered.values()].reduce((sum, result) => sum + result.count, 0);
console.log(`Query contract coverage passed: ${discovered.size} service files, ${totalMutations} apiClient mutations, exact typed inventory coverage.`);
