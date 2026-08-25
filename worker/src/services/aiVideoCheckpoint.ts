import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { atomicWriteFileSync, resolveWithin, sharedAiVideoCheckpointsRoot } from '../config/storage.js';
import type { VideoChapter } from './geminiService.js';
import type { AiOutputLanguage } from './aiGenerationContract.js';

const PIPELINE_VERSION = '5';
const DEFAULT_CHECKPOINT_TTL_MS = 7 * 24 * 60 * 60 * 1_000;
const HASH_DIRECTORY_PATTERN = /^[0-9a-f]{64}$/;

function checkpointDirectory(
  lessonVideoId: string,
  sourceUrl: string,
  outputLanguage: AiOutputLanguage,
  generationRunId: string,
) {
  const lessonKey = crypto.createHash('sha256').update(lessonVideoId).digest('hex');
  const sourceKey = crypto.createHash('sha256')
    .update(`${PIPELINE_VERSION}\0${sourceUrl}\0${outputLanguage}\0${generationRunId}`)
    .digest('hex');
  return path.join(lessonKey, sourceKey);
}

function readCheckpoint(relativePath: string) {
  const checkpointPath = resolveWithin(sharedAiVideoCheckpointsRoot, relativePath);
  return fs.existsSync(checkpointPath) ? fs.readFileSync(checkpointPath, 'utf8') : undefined;
}

function isVideoChapter(chapter: unknown): chapter is VideoChapter {
  if (!chapter || typeof chapter !== 'object') return false;
  const candidate = chapter as Record<string, unknown>;
  return typeof candidate.title === 'string'
    && typeof candidate.startTime === 'number'
    && typeof candidate.endTime === 'number'
    && typeof candidate.summaryText === 'string'
    && typeof candidate.order === 'number';
}

export function createVideoAnalysisCheckpoint(
  lessonVideoId: string,
  sourceUrl: string,
  outputLanguage: AiOutputLanguage,
  generationRunId: string,
) {
  const directory = checkpointDirectory(lessonVideoId, sourceUrl, outputLanguage, generationRunId);
  const lessonDirectory = path.dirname(directory);
  const srtPath = path.join(directory, 'transcription.srt');
  const chaptersPath = path.join(directory, 'chapters.json');

  return {
    transcription: () => readCheckpoint(srtPath),
    saveTranscription: (srtContent: string) => atomicWriteFileSync(sharedAiVideoCheckpointsRoot, srtPath, srtContent, 'utf8'),
    chapters: (): VideoChapter[] | undefined => {
      const serialized = readCheckpoint(chaptersPath);
      if (!serialized) return undefined;
      try {
        const chapters = JSON.parse(serialized);
        if (Array.isArray(chapters) && chapters.every(isVideoChapter)) return chapters;
      } catch (error) {
        if (!(error instanceof SyntaxError)) throw error;
      }
      fs.rmSync(resolveWithin(sharedAiVideoCheckpointsRoot, chaptersPath), { force: true });
      return undefined;
    },
    saveChapters: (chapters: VideoChapter[]) => atomicWriteFileSync(sharedAiVideoCheckpointsRoot, chaptersPath, JSON.stringify(chapters), 'utf8'),
    clear: () => {
      fs.rmSync(resolveWithin(sharedAiVideoCheckpointsRoot, directory), { recursive: true, force: true });
      const lessonPath = resolveWithin(sharedAiVideoCheckpointsRoot, lessonDirectory);
      if (fs.existsSync(lessonPath) && fs.readdirSync(lessonPath).length === 0) fs.rmdirSync(lessonPath);
    },
  };
}

function checkpointDirectories() {
  if (!fs.existsSync(sharedAiVideoCheckpointsRoot)) return [];
  return fs.readdirSync(sharedAiVideoCheckpointsRoot, { withFileTypes: true })
    .filter(lesson => lesson.isDirectory() && HASH_DIRECTORY_PATTERN.test(lesson.name))
    .flatMap(lesson => checkpointDirectoriesForLesson(lesson.name));
}

function checkpointDirectoriesForLesson(lessonName: string) {
  try {
    return fs.readdirSync(path.join(sharedAiVideoCheckpointsRoot, lessonName), { withFileTypes: true })
      .filter(checkpoint => checkpoint.isDirectory() && HASH_DIRECTORY_PATTERN.test(checkpoint.name))
      .map(checkpoint => ({ lessonName, checkpointName: checkpoint.name }));
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') return [];
    throw error;
  }
}

function removeExpiredCheckpoint(relativePath: string, maxAgeMs: number, nowMs: number) {
  const checkpointPath = resolveWithin(sharedAiVideoCheckpointsRoot, relativePath);
  try {
    if (nowMs - fs.statSync(checkpointPath).mtimeMs <= maxAgeMs) return false;
    fs.rmSync(checkpointPath, { recursive: true, force: true });
    return true;
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') return false;
    throw error;
  }
}

function removeEmptyLessonDirectory(lessonName: string) {
  const lessonPath = resolveWithin(sharedAiVideoCheckpointsRoot, lessonName);
  try {
    if (fs.readdirSync(lessonPath).length === 0) fs.rmdirSync(lessonPath);
  } catch (error) {
    const code = (error as NodeJS.ErrnoException).code;
    if (code !== 'ENOENT' && code !== 'ENOTEMPTY') throw error;
  }
}

export function sweepExpiredVideoAnalysisCheckpoints(
  maxAgeMs = DEFAULT_CHECKPOINT_TTL_MS,
  nowMs = Date.now(),
) {
  let removedCount = 0;
  for (const checkpoint of checkpointDirectories()) {
    const relativePath = path.join(checkpoint.lessonName, checkpoint.checkpointName);
    if (!removeExpiredCheckpoint(relativePath, maxAgeMs, nowMs)) continue;
    removedCount += 1;
    removeEmptyLessonDirectory(checkpoint.lessonName);
  }
  return removedCount;
}
