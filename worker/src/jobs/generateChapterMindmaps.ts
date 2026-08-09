import { Job } from 'bullmq';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';
import { generateChapterMindmap } from '../services/geminiService.js';
import { throwIfCancellationRequested } from '../cancellation.js';
import { fetchWithTimeout } from '../services/workerFetch.js';
import { GeminiDeveloperApiError } from '../services/aiProvider.js';
import { sharedMindmapsRoot, sharedPublicRoot } from '../config/storage.js';
import { logWarn } from '../logging.js';

// Resolve worker root reliably regardless of process.cwd()
const __filename = fileURLToPath(import.meta.url);
const __dirname  = path.dirname(__filename);
const workerRoot = path.resolve(__dirname, '../../');

const BACKEND_BASE_URL = process.env.BACKEND_API_URL || 'http://localhost:5245';
const API_KEY = process.env.API_CALLBACK_SECRET || process.env.AI_CALLBACK_SECRET || '';
const QUOTA_RETRY_DELAY_MS = 60_000;
const MAX_CHAPTER_QUOTA_RETRIES = 3;

/** Push progress to backend → SignalR → admin frontend in real time */
async function notifyProgress(jobId: string, percentage: number, stage: string, status = 'active') {
    try {
        await fetchWithTimeout(`${BACKEND_BASE_URL}/api/v1/internal/callbacks/ai-progress`, {
            method: 'POST',
            timeoutMs: 10_000,
            operation: 'mindmap-progress',
            headers: { 'Content-Type': 'application/json', 'X-Internal-Token': API_KEY },
            body: JSON.stringify({ jobId, progress: percentage, status, message: stage }),
        });
    } catch (error) {
        logWarn('mindmap-progress', 'Progress callback failed; generation will continue.', { jobId, error });
    }
}

async function waitForRetryOrCancellation(job: Job, delayMs: number) {
    const pollingIntervalMs = Math.min(1_000, delayMs);
    let remainingMs = delayMs;
    while (remainingMs > 0) {
        await sleep(Math.min(pollingIntervalMs, remainingMs));
        await throwIfCancellationRequested(job);
        remainingMs -= pollingIntervalMs;
    }
}

function sleep(delayMs: number) {
    return new Promise(resolve => setTimeout(resolve, delayMs));
}

function isQuotaError(error: unknown) {
    return error instanceof GeminiDeveloperApiError &&
        error.category === 'quota-exhausted';
}

async function postMindmapResults(lessonVideoId: string, results: Array<{ title: string; imageUrl: string }>) {
    if (results.length === 0) return;

    const webhookResponse = await fetchWithTimeout(
        `${BACKEND_BASE_URL}/api/v1/internal/callbacks/mindmaps-completed`,
        {
            method: 'POST',
            timeoutMs: 10_000,
            operation: 'mindmaps-callback',
            headers: { 'Content-Type': 'application/json', 'X-Internal-Token': API_KEY },
            body: JSON.stringify({ videoId: lessonVideoId, mindmaps: results })
        }
    );
    if (!webhookResponse.ok) {
        const errBody = await webhookResponse.text();
        throw new Error(`Webhook failed ${webhookResponse.status}: ${errBody}`);
    }
}

async function clearFailedChapterRegeneration(chapterId: string) {
    const response = await fetchWithTimeout(
        `${BACKEND_BASE_URL}/api/v1/internal/callbacks/single-mindmap-failed`,
        {
            method: 'POST',
            timeoutMs: 10_000,
            operation: 'single-mindmap-failed-callback',
            headers: { 'Content-Type': 'application/json', 'X-Internal-Token': API_KEY },
            body: JSON.stringify({ chapterId })
        }
    );

    if (!response.ok) {
        throw new Error(`Single mindmap failure callback failed ${response.status}: ${await response.text()}`);
    }
}

async function generateWithQuotaBackoff(
    chapter: ChapterMindmapInput,
    lessonVideoId: string,
    teacherPhotoPaths: string[],
    options: { visualStyles: string[]; teacherStyles: string[] },
    job: Job<GenerateMindmapsJobData>,
) {
    for (let attempt = 0; attempt <= MAX_CHAPTER_QUOTA_RETRIES; attempt++) {
        try {
            return await generateChapterMindmap(chapter, lessonVideoId, teacherPhotoPaths, options);
        } catch (error) {
            if (!isQuotaError(error) || attempt === MAX_CHAPTER_QUOTA_RETRIES) {
                throw error;
            }

            const stage = `كوتة الذكاء الاصطناعي اتملت. انتظار دقيقة ثم إعادة محاولة خريطة الفصل ${chapter.order} (${attempt + 1}/${MAX_CHAPTER_QUOTA_RETRIES}).`;
            console.warn(`[Job ${job.id}] ${stage}`);
            await job.updateProgress({ percentage: 50, stage });
            await notifyProgress(`${lessonVideoId}_mindmaps`, 50, stage);
            await waitForRetryOrCancellation(job, QUOTA_RETRY_DELAY_MS);
        }
    }

    throw new Error(`Failed to generate mindmap for chapter ${chapter.order}.`);
}


interface ChapterMindmapInput {
    title: string;
    summaryText: string;
    order: number;
}

export interface GenerateMindmapsJobData {
    lessonVideoId: string;
    LessonVideoId?: string;
    teacherPhotoUrl?: string;
    TeacherPhotoUrl?: string;
    teacherPhotoUrls?: string[];
    TeacherPhotoUrls?: string[];
    visualStyles?: string[];
    VisualStyles?: string[];
    teacherStyles?: string[];
    TeacherStyles?: string[];
    // Batch mode
    chapters?: ChapterMindmapInput[];
    Chapters?: ChapterMindmapInput[];
    // Single-chapter regeneration mode
    chapterId?: string;
    ChapterId?: string;
    chapter?: ChapterMindmapInput;
    Chapter?: ChapterMindmapInput;
}

/**
 * The BullMQ Job Processor for generating mindmaps.
 * Supports two modes:
 *   - Batch: { lessonVideoId, chapters[], teacherPhotoUrl? }
 *   - Single: { lessonVideoId, chapterId, chapter, teacherPhotoUrl? }
 */
export async function generateMindmapsProcessor(job: Job<GenerateMindmapsJobData>) {
    const lessonVideoId = job.data.lessonVideoId || job.data.LessonVideoId;
    const teacherPhotoUrl = job.data.teacherPhotoUrl || job.data.TeacherPhotoUrl;
    const teacherPhotoUrls = job.data.teacherPhotoUrls || job.data.TeacherPhotoUrls;
    const options = {
        visualStyles: job.data.visualStyles || job.data.VisualStyles || ['editorial-infographic'],
        teacherStyles: job.data.teacherStyles || job.data.TeacherStyles || ['photorealistic'],
    };
    const chapterId = job.data.chapterId || job.data.ChapterId;
    const singleChapter = job.data.chapter || job.data.Chapter;
    const isSingleChapter = !!chapterId && !!singleChapter;
    const chapters = isSingleChapter ? [singleChapter!] : (job.data.chapters || job.data.Chapters || []);

    if (!lessonVideoId) {
        throw new Error('Mindmap job is missing lessonVideoId.');
    }

    console.log(`[Job ${job.id}] Starting ${isSingleChapter ? 'Single-Chapter Regen' : 'Batch'} Mindmaps for VideoId: ${lessonVideoId}`);

    const results: Array<{ title: string; imageUrl: string }> = [];

    try {
        const prepStage = 'تحضير شخصية المدرس...';
        await job.updateProgress({ percentage: 10, stage: prepStage });
        await notifyProgress(`${lessonVideoId}_mindmaps`, 10, prepStage);
        await throwIfCancellationRequested(job);

        // Prepare local paths for teacherPhotoUrls
        let activeTeacherPhotoLocalPaths: string[] = [];
        if (teacherPhotoUrls && Array.isArray(teacherPhotoUrls)) {
            activeTeacherPhotoLocalPaths = teacherPhotoUrls.map(url => {
                const relativeToWwwroot = url.startsWith('/') ? url.substring(1) : url;
                return path.join(sharedPublicRoot, relativeToWwwroot);
            });
        } else if (teacherPhotoUrl) {
            const relativeToWwwroot = teacherPhotoUrl.startsWith('/') ? teacherPhotoUrl.substring(1) : teacherPhotoUrl;
            activeTeacherPhotoLocalPaths = [
                path.join(sharedPublicRoot, relativeToWwwroot)
            ];
        }

        const mindmapsDir = sharedMindmapsRoot;

        const totalChapters = chapters.length;
        if (totalChapters === 0) {
            const noChStage = 'لا توجد فصول لتوليد الصور لها.';
            await job.updateProgress({ percentage: 100, stage: noChStage });
            await notifyProgress(`${lessonVideoId}_mindmaps`, 100, noChStage, 'completed');
            return { success: true, mindmapsGenerated: 0 };
        }

        let completedCount = 0;

        for (const chapter of chapters) {
            await throwIfCancellationRequested(job);

            // Batch generation keeps old mindmaps and only fills missing chapters.
            // Single-chapter regeneration always requests a fresh design and only replaces the old file after success.
            let existingUrl: string | undefined = undefined;
            try {
                if (!isSingleChapter && fs.existsSync(mindmapsDir)) {
                    const files = fs.readdirSync(mindmapsDir);
                    const prefix = `${lessonVideoId}_chapter_${chapter.order}_`;
                    const match = files.find(f => f.startsWith(prefix) && (f.endsWith('.webp') || f.endsWith('.png')));
                    if (match) {
                        existingUrl = `/mindmaps/${match}`;
                        console.log(`[Job ${job.id}] Reusing existing mindmap for chapter ${chapter.order}: ${existingUrl}`);
                    }
                }
            } catch (err) {
                console.error(`[Job ${job.id}] Failed to check existing mindmaps:`, err);
            }

            const generatedUrl = existingUrl || await generateWithQuotaBackoff(chapter, lessonVideoId, activeTeacherPhotoLocalPaths, options, job);
            results.push({ title: chapter.title, imageUrl: generatedUrl });
            completedCount++;
            const progressPct = 10 + Math.floor((completedCount / totalChapters) * 80);
            const chStage = `تم توليد صورة الفصل ${completedCount} من ${totalChapters} (${chapter.title})`;
            await job.updateProgress({ percentage: progressPct, stage: chStage });
            await notifyProgress(`${lessonVideoId}_mindmaps`, progressPct, chStage);
        }

        {
            const saveStage = 'جاري حفظ الخرائط في لوحة التحكم...';
            await job.updateProgress({ percentage: 95, stage: saveStage });
            await notifyProgress(`${lessonVideoId}_mindmaps`, 95, saveStage);
        }
        await throwIfCancellationRequested(job);

        if (isSingleChapter) {
            // ── Single-chapter regeneration: dedicated webhook ────────────────
            const singleResult = results[0];
            if (singleResult) {
                console.log(`[Job ${job.id}] Pushing single mindmap for chapterId: ${chapterId}...`);
                const webhookResponse = await fetchWithTimeout(
                    `${BACKEND_BASE_URL}/api/v1/internal/callbacks/single-mindmap-completed`,
                    {
                        method: 'POST',
                        timeoutMs: 10_000,
                        operation: 'single-mindmap-callback',
                        headers: { 'Content-Type': 'application/json', 'X-Internal-Token': API_KEY },
                        body: JSON.stringify({ chapterId, imageUrl: singleResult.imageUrl })
                    }
                );
                if (!webhookResponse.ok) {
                    const errBody = await webhookResponse.text();
                    throw new Error(`Single mindmap webhook failed ${webhookResponse.status}: ${errBody}`);
                }
            } else {
                console.warn(`[Job ${job.id}] No image generated for chapterId ${chapterId}, skipping webhook.`);
            }
        } else {
            // ── Batch (full video): existing webhook ──────────────────────────
            console.log(`[Job ${job.id}] Pushing ${results.length} batch mindmaps to backend...`);
            await postMindmapResults(lessonVideoId, results);
        }

        console.log(`[Job ${job.id}] Successfully generated ${results.length} mindmaps.`);
        const doneStage = 'اكتمل توليد الخرائط الذهنية بنجاح.';
        await job.updateProgress({ percentage: 100, stage: doneStage });
        await notifyProgress(`${lessonVideoId}_mindmaps`, 100, doneStage, 'completed');
        return { success: true, mindmapsGenerated: results.length };

    } catch (error) {
        console.error(`[Job ${job.id}] Failed generating mindmaps:`, error);
        if (isSingleChapter) {
            try {
                await clearFailedChapterRegeneration(chapterId!);
            } catch (callbackError) {
                logWarn('single-mindmap-failed', 'Failed to clear chapter regeneration state.', { chapterId, callbackError });
            }
        }
        throw error;
    }
}

export default generateMindmapsProcessor;
