import fs from 'node:fs';
import path from 'node:path';

const repositoryRoot = path.resolve(import.meta.dirname, '../..');
const backendRoot = path.join(repositoryRoot, 'backend/src');
const hookPath = path.join(repositoryRoot, 'frontend/src/hooks/usePlatformEvents.ts');

function listFiles(directory, extension) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const entryPath = path.join(directory, entry.name);
    return entry.isDirectory()
      ? listFiles(entryPath, extension)
      : entry.name.endsWith(extension) ? [entryPath] : [];
  });
}

function outboxInitializers(source) {
  const starts = [...source.matchAll(/new\s+(?:NaderGorge\.Domain\.Entities\.)?OutboxEvent\s*\{/g)];
  return starts.map((match) => {
    let depth = 0;
    let inString = false;
    let escaped = false;

    for (let index = match.index; index < source.length; index++) {
      const character = source[index];
      if (inString) {
        if (escaped) escaped = false;
        else if (character === '\\') escaped = true;
        else if (character === '"') inString = false;
        continue;
      }

      if (character === '"') inString = true;
      else if (character === '{') depth++;
      else if (character === '}' && --depth === 0) return source.slice(match.index, index + 1);
    }

    throw new Error('Unclosed OutboxEvent initializer.');
  });
}

function eventTypes(initializer) {
  const ternary = initializer.match(/Type\s*=\s*[^?]+\?\s*"([^"]+)"\s*:\s*"([^"]+)"/);
  if (ternary) return [ternary[1], ternary[2]];

  const literal = initializer.match(/Type\s*=\s*"([^"]+)"/);
  return literal ? [literal[1]] : [];
}

const producerTypes = new Set();
const untargetedProducers = [];

for (const filePath of listFiles(backendRoot, '.cs')) {
  const source = fs.readFileSync(filePath, 'utf8');
  for (const initializer of outboxInitializers(source)) {
    const types = eventTypes(initializer);
    types.forEach((type) => producerTypes.add(type));
    if (types.length > 0 && !/Target(?:UserId|Group)\s*=/.test(initializer)) {
      untargetedProducers.push(`${path.relative(repositoryRoot, filePath)}: ${types.join(', ')}`);
    }
  }
}

const hookSource = fs.readFileSync(hookPath, 'utf8');
const listenerTypes = new Set(
  [...hookSource.matchAll(/sharedConnection\.on\('([^']+)'/g)].map((match) => match[1])
);
const registeredListeners = new Set(
  [...hookSource.matchAll(/listeners\.([A-Za-z0-9]+)\.add\(/g)].map((match) => match[1])
);
const cleanedListeners = new Set(
  [...hookSource.matchAll(/listeners\.([A-Za-z0-9]+)\.delete\(/g)].map((match) => match[1])
);
const missingListeners = [...producerTypes].filter((type) => !listenerTypes.has(type)).sort();
const missingProducers = [...listenerTypes].filter((type) => !producerTypes.has(type)).sort();
const missingCleanup = [...registeredListeners]
  .filter((type) => !cleanedListeners.has(type))
  .sort();
const orphanedCleanup = [...cleanedListeners]
  .filter((type) => !registeredListeners.has(type))
  .sort();

if (
  missingListeners.length ||
  missingProducers.length ||
  untargetedProducers.length ||
  missingCleanup.length ||
  orphanedCleanup.length
) {
  console.error(JSON.stringify({
    missingListeners,
    missingProducers,
    untargetedProducers,
    missingCleanup,
    orphanedCleanup,
  }, null, 2));
  process.exit(1);
}

console.log(`Platform event contracts verified: ${producerTypes.size} producers, ${listenerTypes.size} listeners.`);
