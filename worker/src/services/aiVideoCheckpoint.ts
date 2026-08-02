import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { atomicWriteFileSync, resolveWithin, sharedAiVideoCheckpointsRoot } from '../config/storage.js';
import type { VideoChapter } from './geminiService.js';

const PIPELINE_VERSION = '3';

function checkpointDirectory(lessonVideoId: string, sourceUrl: string) {
  const lessonKey = crypto.createHash('sha256').update(lessonVideoId).digest('hex');
  const sourceKey = crypto.createHash('sha256').update(`${PIPELINE_VERSION}\0${sourceUrl}`).digest('hex');
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

export function createVideoAnalysisCheckpoint(lessonVideoId: string, sourceUrl: string) {
  const directory = checkpointDirectory(lessonVideoId, sourceUrl);
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
