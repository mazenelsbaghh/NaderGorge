import fs from 'node:fs';
import path from 'node:path';
import { Readable } from 'node:stream';
import { pipeline } from 'node:stream/promises';
import { fileURLToPath } from 'node:url';
import ytDlp from 'youtube-dl-exec';
import { execFileWithTimeout, redactExternalText, WorkerExternalError } from '../services/workerFetch.js';
import { normalizePublicYouTubeUrl } from './youtubeSource.js';

const ytDlpPath = (ytDlp as unknown as { constants: { YOUTUBE_DL_PATH: string } }).constants.YOUTUBE_DL_PATH;
const moduleDirectory = path.dirname(fileURLToPath(import.meta.url));
const workerRoot = path.resolve(moduleDirectory, '../../');
const SAFE_OUTPUT_NAME = /^[A-Za-z0-9._-]{1,180}$/;
const BUNNY_LIBRARY_ID = /^[1-9]\d{0,18}$/;
const BUNNY_VIDEO_GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const SCOPED_BUNNY_VIDEO = /^([1-9]\d{0,18})\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i;
const VK_STORED_VIDEO_ID = /^oid=(-?\d+)&id=(\d+)$/;

export const BUNNY_ANALYSIS_ACCESS_MESSAGE =
  'تعذر تجهيز أصل فيديو Bunny للتحليل. فعّل الاحتفاظ بالملف الأصلي وراجِع إعدادات وصول Bunny ثم أعد المحاولة.';

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
  const scopedMatch = SCOPED_BUNNY_VIDEO.exec(source);
  if (scopedMatch) {
    return `https://iframe.mediadelivery.net/embed/${scopedMatch[1]}/${scopedMatch[2]}`;
  }

  // Bare GUIDs only remain in jobs queued before library-scoped Bunny sources.
  if (!BUNNY_VIDEO_GUID.test(source)) return undefined;
  const legacyLibraryId = process.env.BUNNY_STREAM_LIBRARY_ID?.trim() || '';
  if (!BUNNY_LIBRARY_ID.test(legacyLibraryId)) {
    throw new WorkerExternalError(
      'implementation',
      false,
      'تعذر تحليل فيديو Bunny قديم لأن رقم المكتبة غير مُهيأ.',
    );
  }
  return `https://iframe.mediadelivery.net/embed/${legacyLibraryId}/${source}`;
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
  } catch {
    throw new WorkerExternalError(
      'conversion',
      false,
      'تم تنزيل الفيديو لكن تعذر تجهيز مساره الصوتي.',
    );
  } finally {
    fs.rmSync(sourcePath, { force: true });
  }
}

/** True when the queue value is a persisted Bunny video reference, not a public URL. */
export function isStoredBunnyVideoSource(source: string) {
  const trimmed = source.trim();
  return SCOPED_BUNNY_VIDEO.test(trimmed) || BUNNY_VIDEO_GUID.test(trimmed);
}

function bunnyInternalMediaUrl(lessonVideoId: string, generationRunId: string) {
  if (!BUNNY_VIDEO_GUID.test(lessonVideoId) || !BUNNY_VIDEO_GUID.test(generationRunId)) {
    throw new WorkerExternalError('implementation', false, 'بيانات مهمة تحليل Bunny غير صالحة.');
  }

  try {
    return new URL(
      `/api/v1/internal/ai-media/bunny/${lessonVideoId}/runs/${generationRunId}/original`,
      process.env.BACKEND_API_URL || 'http://localhost:5245',
    ).toString();
  } catch {
    throw new WorkerExternalError('implementation', false, 'عنوان خدمة التحليل الداخلية غير صالح.');
  }
}

function internalMediaToken() {
  const token = process.env.AI_MEDIA_RELAY_SECRET || '';
  if (!token) {
    throw new WorkerExternalError('implementation', false, 'رمز الوصول الداخلي لتحليل الفيديو غير مُهيأ.');
  }
  return token;
}

function bunnyInternalMediaFailure(status: number) {
  if ([401, 403, 404, 409, 422].includes(status)) {
    return new WorkerExternalError(
      'rejected',
      false,
      BUNNY_ANALYSIS_ACCESS_MESSAGE,
    );
  }
  if (status === 429 || status >= 500) {
    return new WorkerExternalError(
      'provider',
      true,
      'تعذر الوصول إلى مصدر Bunny مؤقتًا. ستتم إعادة المحاولة تلقائيًا.',
    );
  }
  return new WorkerExternalError(
    'provider',
    true,
    'تعذر تجهيز مصدر Bunny للتحليل. ستتم إعادة المحاولة تلقائيًا.',
  );
}

async function downloadBunnyOriginalFromPlatform(
  lessonVideoId: string,
  generationRunId: string,
  destinationPath: string,
) {
  const temporaryPath = `${destinationPath}.${process.pid}.${Date.now()}.part`;
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), downloadTimeoutMs());

  try {
    const response = await fetch(bunnyInternalMediaUrl(lessonVideoId, generationRunId), {
      headers: { 'X-Internal-Token': internalMediaToken() },
      signal: controller.signal,
    });
    if (!response.ok) throw bunnyInternalMediaFailure(response.status);
    if (!response.body) {
      throw new WorkerExternalError(
        'provider',
        true,
        'انتهى مصدر Bunny دون محتوى قابل للتحليل. ستتم إعادة المحاولة تلقائيًا.',
      );
    }

    const contentType = response.headers.get('content-type') || '';
    if (!/^(video|audio)\//i.test(contentType) && !/^application\/octet-stream$/i.test(contentType)) {
      throw new WorkerExternalError(
        'provider',
        false,
        'أعاد مصدر Bunny نوع ملف غير صالح للتحليل.',
      );
    }

    await pipeline(
      Readable.fromWeb(response.body as never),
      fs.createWriteStream(temporaryPath, { flags: 'wx', mode: 0o600 }),
    );
    fs.renameSync(temporaryPath, destinationPath);
  } catch (error) {
    fs.rmSync(temporaryPath, { force: true });
    fs.rmSync(destinationPath, { force: true });
    if (error instanceof WorkerExternalError) throw error;
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new WorkerExternalError(
        'timeout',
        true,
        'انتهت مهلة تنزيل مصدر Bunny للتحليل. ستتم إعادة المحاولة تلقائيًا.',
      );
    }
    throw downloaderFailure(error);
  } finally {
    clearTimeout(timeout);
  }
}

/**
 * Retrieves a Bunny original through the platform's internal relay, then extracts
 * the audio locally. No Bunny CDN URL or credential is ever given to the worker.
 */
export async function extractAudioFromInternalBunnyVideo(
  lessonVideoId: string,
  generationRunId: string,
  outputFileName: string,
): Promise<string> {
  if (!SAFE_OUTPUT_NAME.test(outputFileName)) {
    throw new WorkerExternalError('implementation', false, 'معرّف مهمة تحليل الفيديو غير صالح.');
  }

  const tempDirectory = path.join(workerRoot, '.tmp');
  fs.mkdirSync(tempDirectory, { recursive: true });
  const expectedMp3 = path.join(tempDirectory, `${outputFileName}.mp3`);
  if (fs.existsSync(expectedMp3)) return expectedMp3;

  const sourcePath = path.join(tempDirectory, `${outputFileName}.bunny-original`);
  await downloadBunnyOriginalFromPlatform(lessonVideoId, generationRunId, sourcePath);
  await convertToMp3(sourcePath, expectedMp3);
  return expectedMp3;
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
