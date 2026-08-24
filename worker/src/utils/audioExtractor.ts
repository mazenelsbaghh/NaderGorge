import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import ytDlp from 'youtube-dl-exec';
import { execFileWithTimeout, redactExternalText, WorkerExternalError } from '../services/workerFetch.js';
import { normalizePublicYouTubeUrl } from './youtubeSource.js';

const ytDlpPath = (ytDlp as unknown as { constants: { YOUTUBE_DL_PATH: string } }).constants.YOUTUBE_DL_PATH;
const moduleDirectory = path.dirname(fileURLToPath(import.meta.url));
const workerRoot = path.resolve(moduleDirectory, '../../');
const SAFE_OUTPUT_NAME = /^[A-Za-z0-9._-]{1,180}$/;
const BUNNY_VIDEO_ID = /^[a-f0-9-]{32,36}$/i;
const VK_STORED_VIDEO_ID = /^oid=(-?\d+)&id=(\d+)$/;

function downloadTimeoutMs() {
  const configured = Number.parseInt(process.env.WORKER_DOWNLOAD_TIMEOUT_MS || '600000', 10);
  return Number.isFinite(configured) && configured > 0 ? configured : 600_000;
}

function downloaderFailure(error: unknown) {
  if (error instanceof WorkerExternalError && error.category === 'timeout') return error;

  const diagnostic = redactExternalText(error instanceof Error ? error.message : error);
  if (/unsupported url|video unavailable|private video|members[- ]only|not available in your country|login required/i.test(diagnostic)) {
    return new WorkerExternalError(
      'rejected',
      false,
      'تعذر الوصول إلى مصدر الفيديو. تأكد أن الفيديو متاح وأن الرابط صحيح.',
    );
  }
  if (/429|too many requests|http error 5\d\d|connection|network|temporar|remote end closed/i.test(diagnostic)) {
    return new WorkerExternalError(
      'network',
      true,
      'تعذر تنزيل الفيديو مؤقتًا. ستتم إعادة المحاولة تلقائيًا.',
    );
  }
  return new WorkerExternalError(
    'provider',
    true,
    'تعذر تنزيل الفيديو من مزود المحتوى مؤقتًا. ستتم إعادة المحاولة تلقائيًا.',
  );
}

function normalizedVkDownloadUrl(source: string) {
  const match = VK_STORED_VIDEO_ID.exec(source);
  if (!match) return undefined;
  return `https://vk.com/video${match[1]}_${match[2]}`;
}

function bunnyDownloadSource(source: string) {
  if (!BUNNY_VIDEO_ID.test(source)) return undefined;
  const libraryId = process.env.BUNNY_STREAM_LIBRARY_ID?.trim();
  if (!libraryId) {
    throw new WorkerExternalError(
      'implementation',
      false,
      'إعداد مكتبة Bunny Stream غير مكتمل.',
    );
  }
  return `https://iframe.mediadelivery.net/embed/${libraryId}/${source}`;
}

function resolveDownloadSource(sourceUrl: string) {
  const trimmed = sourceUrl.trim();
  if (normalizePublicYouTubeUrl(trimmed)) {
    throw new WorkerExternalError(
      'implementation',
      false,
      'يجب تحليل فيديو YouTube مباشرة عبر مزود الذكاء الاصطناعي.',
    );
  }

  const vkUrl = normalizedVkDownloadUrl(trimmed);
  if (vkUrl) return { url: vkUrl, isBunny: false };
  const bunnyUrl = bunnyDownloadSource(trimmed);
  return bunnyUrl ? { url: bunnyUrl, isBunny: true } : { url: trimmed, isBunny: false };
}

function downloaderArguments(url: string, outputTemplate: string, isBunny: boolean) {
  const args = [
    url,
    '--extract-audio',
    '--audio-format', 'mp3',
    '--audio-quality', '5',
    '-o', outputTemplate,
    '--no-playlist',
    '--newline',
    '--js-runtimes', `node:${process.execPath}`,
  ];
  if (isBunny) args.push('--referer', 'https://admin.massar-academy.net/');
  return args;
}

async function convertToMp3(sourcePath: string, destinationPath: string) {
  try {
    await execFileWithTimeout('ffmpeg', [
      '-i', sourcePath,
      '-vn',
      '-ar', '16000',
      '-ac', '1',
      '-b:a', '48k',
      '-y',
      destinationPath,
    ], downloadTimeoutMs());
    fs.rmSync(sourcePath, { force: true });
  } catch {
    throw new WorkerExternalError(
      'conversion',
      false,
      'تم تنزيل الفيديو لكن تعذر تجهيز مساره الصوتي.',
    );
  }
}

/** Downloads non-YouTube lesson media and extracts a compressed MP3 track. */
export async function extractAudioFromVideo(sourceUrl: string, outputFileName: string): Promise<string> {
  if (!SAFE_OUTPUT_NAME.test(outputFileName)) {
    throw new WorkerExternalError('implementation', false, 'معرّف مهمة تحليل الفيديو غير صالح.');
  }

  const { url, isBunny } = resolveDownloadSource(sourceUrl);
  const tempDirectory = path.join(workerRoot, '.tmp');
  fs.mkdirSync(tempDirectory, { recursive: true });
  const expectedMp3 = path.join(tempDirectory, `${outputFileName}.mp3`);
  if (fs.existsSync(expectedMp3)) return expectedMp3;

  const filesBefore = new Set(fs.readdirSync(tempDirectory));
  const outputTemplate = path.join(tempDirectory, outputFileName);

  try {
    const { stdout, stderr } = await execFileWithTimeout(
      ytDlpPath,
      downloaderArguments(url, outputTemplate, isBunny),
      downloadTimeoutMs(),
    );
    if (stdout) console.log('[Video download] yt-dlp completed.', { detail: redactExternalText(stdout) });
    if (stderr) console.warn('[Video download] yt-dlp warning.', { detail: redactExternalText(stderr) });
  } catch (error) {
    throw downloaderFailure(error);
  }

  if (fs.existsSync(expectedMp3)) return expectedMp3;

  const candidates = fs.readdirSync(tempDirectory)
    .filter((file) => !filesBefore.has(file) && file.startsWith(outputFileName));
  const candidate = candidates.find((file) => file.endsWith('.mp3')) ?? candidates[0];
  if (!candidate) {
    throw new WorkerExternalError(
      'provider',
      true,
      'انتهى تنزيل الفيديو دون إنتاج ملف صوتي. ستتم إعادة المحاولة تلقائيًا.',
    );
  }

  const candidatePath = path.join(tempDirectory, candidate);
  if (candidate.endsWith('.mp3')) {
    if (candidatePath !== expectedMp3) fs.renameSync(candidatePath, expectedMp3);
    return expectedMp3;
  }

  await convertToMp3(candidatePath, expectedMp3);
  return expectedMp3;
}
