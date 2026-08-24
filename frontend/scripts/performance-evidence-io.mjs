import { createHash, randomUUID } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

function isPathInside(parentPath, candidatePath) {
  const relativePath = path.relative(parentPath, candidatePath);
  return relativePath === '' || (
    relativePath !== '..' &&
    !relativePath.startsWith(`..${path.sep}`) &&
    !path.isAbsolute(relativePath)
  );
}

export function resolveRawEvidenceOutput(repositoryRoot, requestedPath, defaultFileName) {
  const rawEvidenceRoot = path.join(
    repositoryRoot,
    'artifacts/performance-167/final/raw',
  );
  const effectivePath = requestedPath || path.join(rawEvidenceRoot, defaultFileName);
  const outputPath = path.isAbsolute(effectivePath)
    ? path.normalize(effectivePath)
    : path.resolve(repositoryRoot, effectivePath);

  if (path.extname(outputPath).toLowerCase() !== '.json') {
    throw new Error('Performance evidence output must be a JSON file.');
  }
  if (!isPathInside(rawEvidenceRoot, outputPath)) {
    throw new Error(
      'Raw performance evidence must stay under artifacts/performance-167/final/raw.',
    );
  }

  return outputPath;
}

function requireRegularNonSymlink(filePath, label) {
  const fileStat = fs.lstatSync(filePath);
  if (fileStat.isSymbolicLink() || !fileStat.isFile()) {
    throw new Error(`${label} must be a regular non-symlink file: ${filePath}`);
  }
}

function ensureDirectoryChainWithoutSymlinks(directoryPath) {
  const parsedPath = path.parse(path.resolve(directoryPath));
  const segments = path.resolve(directoryPath)
    .slice(parsedPath.root.length)
    .split(path.sep)
    .filter(Boolean);
  let currentPath = parsedPath.root;

  for (const segment of segments) {
    currentPath = path.join(currentPath, segment);
    let currentStat;
    try {
      currentStat = fs.lstatSync(currentPath);
    } catch (error) {
      if (error?.code !== 'ENOENT') throw error;
      try {
        fs.mkdirSync(currentPath, { mode: 0o700 });
      } catch (mkdirError) {
        if (mkdirError?.code !== 'EEXIST') throw mkdirError;
      }
      currentStat = fs.lstatSync(currentPath);
    }

    if (currentStat.isSymbolicLink()) {
      throw new Error(`Refusing symlink evidence directory: ${currentPath}`);
    }
    if (!currentStat.isDirectory()) {
      throw new Error(`Evidence path component is not a directory: ${currentPath}`);
    }
  }
}

export function readPerformanceSourceBinding({
  repositoryRoot,
  manifestPath,
}) {
  const effectiveManifestPath = manifestPath || path.join(
    repositoryRoot,
    'artifacts/performance-167/final/raw/source-manifest.json',
  );
  const absoluteManifestPath = path.isAbsolute(effectiveManifestPath)
    ? path.normalize(effectiveManifestPath)
    : path.resolve(repositoryRoot, effectiveManifestPath);
  requireRegularNonSymlink(absoluteManifestPath, 'Source manifest');

  const manifestBytes = fs.readFileSync(absoluteManifestPath);
  const manifest = JSON.parse(manifestBytes.toString('utf8'));
  const normalizedReleaseId = String(manifest.releaseId ?? '').trim();
  if (!/^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$/.test(normalizedReleaseId)) {
    throw new Error('Source manifest releaseId is missing or invalid.');
  }
  if (!/^[0-9a-f]{40}$/i.test(String(manifest.gitCommit ?? ''))) {
    throw new Error('Source manifest gitCommit is missing or invalid.');
  }
  if (!/^[0-9a-f]{64}$/i.test(String(manifest.sourceStateSha256 ?? ''))) {
    throw new Error('Source manifest sourceStateSha256 is missing or invalid.');
  }
  if (typeof manifest.dirtySourceSnapshot !== 'boolean') {
    throw new Error('Source manifest dirtySourceSnapshot is missing or invalid.');
  }
  if (
    typeof manifest.sourceDigestAlgorithm !== 'string' ||
    !manifest.sourceDigestAlgorithm.trim()
  ) {
    throw new Error('Source manifest sourceDigestAlgorithm is missing.');
  }

  return {
    releaseId: normalizedReleaseId,
    gitCommit: manifest.gitCommit,
    sourceStateSha256: manifest.sourceStateSha256,
    dirtySourceSnapshot: manifest.dirtySourceSnapshot,
    sourceDigestAlgorithm: manifest.sourceDigestAlgorithm,
    manifestSha256: createHash('sha256').update(manifestBytes).digest('hex'),
  };
}

function rejectExistingOutput(outputPath) {
  let outputStat;
  try {
    outputStat = fs.lstatSync(outputPath);
  } catch (error) {
    if (error?.code === 'ENOENT') return;
    throw error;
  }

  if (outputStat.isSymbolicLink()) {
    throw new Error(`Refusing symlink evidence output: ${outputPath}`);
  }
  if (!outputStat.isFile()) {
    throw new Error(`Refusing non-regular evidence output: ${outputPath}`);
  }
  throw new Error(`Refusing to overwrite existing performance evidence: ${outputPath}`);
}

export function writeJsonEvidenceCreateNew(outputPath, evidence) {
  ensureDirectoryChainWithoutSymlinks(path.dirname(outputPath));
  const parentStat = fs.statSync(path.dirname(outputPath));
  if (!parentStat.isDirectory()) {
    throw new Error(`Evidence parent is not a directory: ${path.dirname(outputPath)}`);
  }
  rejectExistingOutput(outputPath);

  const temporaryPath = path.join(
    path.dirname(outputPath),
    `.${path.basename(outputPath)}.${process.pid}.${randomUUID()}.tmp`,
  );
  let fileDescriptor;
  try {
    fileDescriptor = fs.openSync(temporaryPath, 'wx', 0o600);
    fs.writeFileSync(fileDescriptor, `${JSON.stringify(evidence, null, 2)}\n`, 'utf8');
    fs.fsyncSync(fileDescriptor);
    fs.closeSync(fileDescriptor);
    fileDescriptor = undefined;

    try {
      fs.linkSync(temporaryPath, outputPath);
    } catch (error) {
      if (error?.code === 'EEXIST') rejectExistingOutput(outputPath);
      throw error;
    }

    requireRegularNonSymlink(outputPath, 'Atomic performance evidence output');
  } finally {
    if (fileDescriptor !== undefined) fs.closeSync(fileDescriptor);
    try {
      fs.unlinkSync(temporaryPath);
    } catch (error) {
      if (error?.code !== 'ENOENT') throw error;
    }
  }
}
