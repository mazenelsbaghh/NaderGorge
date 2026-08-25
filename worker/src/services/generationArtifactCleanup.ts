import { promises as fs } from 'node:fs';
import type { Stats } from 'node:fs';
import path from 'node:path';
import { WorkerExternalError } from './workerFetch.js';

const VIDEO_ID_PATTERN = /^[A-Za-z0-9-]{1,80}$/;
const RUN_ID_SOURCE =
  '(?:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|legacy-[0-9a-f]{32})';
const RUN_ID_PATTERN = new RegExp(`^${RUN_ID_SOURCE}$`, 'i');
const MAX_FILE_NAME_LENGTH = 240;

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function requireSafeIdentifier(
  value: string,
  pattern: RegExp,
  label: string,
): void {
  if (!pattern.test(value)) {
    throw new Error(`Invalid ${label}`);
  }
}

async function listRegularFiles(directory: string): Promise<string[]> {
  let entries: string[];
  try {
    entries = await fs.readdir(directory);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
      return [];
    }
    throw error;
  }

  const regularFiles: string[] = [];
  for (const entry of entries) {
    if (
      entry.length === 0 ||
      entry.length > MAX_FILE_NAME_LENGTH ||
      entry === '.' ||
      entry === '..'
    ) {
      continue;
    }

    const candidatePath = path.join(directory, entry);
    let stat: Stats;
    try {
      stat = await fs.lstat(candidatePath);
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
        continue;
      }
      throw error;
    }

    if (stat.isFile() && !stat.isSymbolicLink()) {
      regularFiles.push(entry);
    }
  }

  return regularFiles;
}

async function removeRegularFile(directory: string, fileName: string): Promise<void> {
  const candidatePath = path.join(directory, fileName);
  let stat: Stats;
  try {
    stat = await fs.lstat(candidatePath);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
      return;
    }
    throw error;
  }

  if (!stat.isFile() || stat.isSymbolicLink()) {
    return;
  }

  try {
    await fs.unlink(candidatePath);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code !== 'ENOENT') {
      throw error;
    }
  }
}

export function readCallbackAcceptance(payload: unknown): boolean | undefined {
  if (!payload || typeof payload !== 'object') {
    return undefined;
  }

  const data = (payload as { data?: unknown }).data;
  if (!data || typeof data !== 'object') {
    return undefined;
  }

  const accepted = (data as { accepted?: unknown }).accepted;
  return typeof accepted === 'boolean' ? accepted : undefined;
}

export async function readCallbackResponseAcceptance(
  response: Response,
  generationRunId: string | undefined,
): Promise<boolean | undefined> {
  let payload: unknown;
  try {
    payload = await response.json();
  } catch {
    if (generationRunId) throw missingFencedReceipt();
    return undefined;
  }

  const accepted = readCallbackAcceptance(payload);
  if (accepted === undefined && generationRunId) throw missingFencedReceipt();
  return accepted;
}

function missingFencedReceipt() {
  return new WorkerExternalError(
    'provider',
    true,
    'الخادم لم يؤكد استلام نتيجة التوليد. ستتم إعادة المحاولة تلقائيًا.',
  );
}

export async function reconcileAnalysisArtifacts(
  subtitlesDirectory: string,
  videoId: string,
  artifactRunId: string,
  accepted: boolean | undefined,
): Promise<void> {
  if (accepted === undefined) {
    return;
  }

  requireSafeIdentifier(videoId, VIDEO_ID_PATTERN, 'video id');
  requireSafeIdentifier(artifactRunId, RUN_ID_PATTERN, 'artifact run id');

  const matcher = new RegExp(
    `^${escapeRegExp(videoId)}_run_(${RUN_ID_SOURCE})\\.srt(?:\\.\\d+\\.\\d+\\.tmp)?$`,
    'i',
  );

  for (const fileName of await listRegularFiles(subtitlesDirectory)) {
    const normalizedName = fileName.startsWith('.') ? fileName.slice(1) : fileName;
    const match = matcher.exec(normalizedName);
    if (!match) {
      continue;
    }

    const isCurrentRun = match[1]!.toLowerCase() === artifactRunId.toLowerCase();
    if ((accepted && !isCurrentRun) || (!accepted && isCurrentRun)) {
      await removeRegularFile(subtitlesDirectory, fileName);
    }
  }
}

interface MindmapArtifact {
  fileName: string;
  runId: string;
  chapterOrder: number;
}

async function listMindmapArtifacts(
  mindmapsDirectory: string,
  videoId: string,
): Promise<MindmapArtifact[]> {
  requireSafeIdentifier(videoId, VIDEO_ID_PATTERN, 'video id');

  const matcher = new RegExp(
    `^${escapeRegExp(videoId)}_run_(${RUN_ID_SOURCE})_chapter_(\\d{1,6})_[^/]{1,120}\\.(?:png|webp)(?:\\.\\d+\\.\\d+\\.tmp)?$`,
    'i',
  );
  const artifacts: MindmapArtifact[] = [];

  for (const fileName of await listRegularFiles(mindmapsDirectory)) {
    const normalizedName = fileName.startsWith('.') ? fileName.slice(1) : fileName;
    const match = matcher.exec(normalizedName);
    if (!match) {
      continue;
    }

    artifacts.push({
      fileName,
      runId: match[1]!,
      chapterOrder: Number(match[2]!),
    });
  }

  return artifacts;
}

export async function reconcileMindmapArtifacts(
  mindmapsDirectory: string,
  videoId: string,
  artifactRunId: string,
  accepted: boolean | undefined,
  chapterOrder?: number,
): Promise<void> {
  if (accepted === undefined) {
    return;
  }

  requireSafeIdentifier(artifactRunId, RUN_ID_PATTERN, 'artifact run id');
  if (
    chapterOrder !== undefined &&
    (!Number.isSafeInteger(chapterOrder) || chapterOrder < 0 || chapterOrder > 999_999)
  ) {
    throw new Error('Invalid chapter order');
  }

  for (const artifact of await listMindmapArtifacts(mindmapsDirectory, videoId)) {
    if (
      chapterOrder !== undefined &&
      artifact.chapterOrder !== chapterOrder
    ) {
      continue;
    }

    const isCurrentRun =
      artifact.runId.toLowerCase() === artifactRunId.toLowerCase();
    if ((accepted && !isCurrentRun) || (!accepted && isCurrentRun)) {
      await removeRegularFile(mindmapsDirectory, artifact.fileName);
    }
  }
}

export async function cleanupCurrentMindmapArtifacts(
  mindmapsDirectory: string,
  videoId: string,
  artifactRunId: string,
): Promise<void> {
  await reconcileMindmapArtifacts(
    mindmapsDirectory,
    videoId,
    artifactRunId,
    false,
  );
}
