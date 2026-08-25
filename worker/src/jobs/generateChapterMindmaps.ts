import { Job, UnrecoverableError } from 'bullmq';
import path from 'path';
import fs from 'fs';
import { generateChapterMindmap, mindmapArtifactPrefix } from '../services/geminiService.js';
import { throwIfGenerationCancellationRequested } from './generationCancellation.js';
import { fetchWithTimeout, WorkerExternalError } from '../services/workerFetch.js';
import { sharedMindmapsRoot, sharedPublicRoot } from '../config/storage.js';
import { logWarn } from '../logging.js';
import { parseAiOutputLanguage, resolveGenerationRun } from '../services/aiGenerationContract.js';
import {
    cleanupCurrentMindmapArtifacts,
    readCallbackResponseAcceptance,
    reconcileMindmapArtifacts,
} from '../services/generationArtifactCleanup.js';
import { isFinalJobAttempt } from '../utils/jobTempFiles.js';

const BACKEND_BASE_URL = process.env.BACKEND_API_URL || 'http://localhost:5245';
const API_KEY = process.env.API_CALLBACK_SECRET || process.env.AI_CALLBACK_SECRET || '';

/** Push progress to backend → SignalR → admin frontend in real time */
interface MindmapProgressUpdate {
    jobId: string,
    generationRunId: string | undefined,
    percentage: number,
    stage: string,
    status?: string,
}

async function notifyProgress(update: MindmapProgressUpdate) {
    const { jobId, generationRunId, percentage, stage, status = 'active' } = update;
    try {
        await fetchWithTimeout(`${BACKEND_BASE_URL}/api/v1/internal/callbacks/ai-progress`, {
            method: 'POST',
            timeoutMs: 10_000,
            operation: 'mindmap-progress',
            headers: { 'Content-Type': 'application/json', 'X-Internal-Token': API_KEY },
            body: JSON.stringify({
                jobId,
                ...(generationRunId ? { generationRunId } : {}),
                progress: percentage,
                status,
                message: stage,
            }),
        });
    } catch (error) {
        logWarn('mindmap-progress', 'Progress callback failed; generation will continue.', {
            jobId,
            errorName: error instanceof Error ? error.name : 'UnknownError',
        });
    }
}

function callbackFailure(status: number) {
    const retryable = status === 408 || status === 429 || status >= 500;
    return new WorkerExternalError(
        retryable ? 'provider' : 'rejected',
        retryable,
        retryable
            ? 'تعذر حفظ الخرائط الذهنية مؤقتًا. ستتم إعادة المحاولة تلقائيًا.'
            : 'رفضت المنصة حفظ الخرائط الذهنية. ابدأ طلب توليد جديد من لوحة التحكم.',
    );
}

async function postMindmapResults(
    lessonVideoId: string,
    generationRunId: string | undefined,
    results: MindmapCallbackResult[],
) {
    if (results.length === 0) return undefined;

    const webhookResponse = await fetchWithTimeout(
        `${BACKEND_BASE_URL}/api/v1/internal/callbacks/mindmaps-completed`,
        {
            method: 'POST',
            timeoutMs: 10_000,
            maxResponseBytes: 16_384,
            operation: 'mindmaps-callback',
            headers: { 'Content-Type': 'application/json', 'X-Internal-Token': API_KEY },
            body: JSON.stringify({
                videoId: lessonVideoId,
                ...(generationRunId ? { generationRunId } : {}),
                mindmaps: results,
            })
        }
    );
    if (!webhookResponse.ok) {
        throw callbackFailure(webhookResponse.status);
    }
    return readCallbackResponseAcceptance(webhookResponse, generationRunId);
}

interface ChapterMindmapInput {
    chapterId?: string;
    title: string;
    summaryText: string;
    order: number;
}

interface MindmapCallbackResult {
    chapterId?: string;
    title: string;
    imageUrl: string;
    order: number;
}

const CHAPTER_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function orderedBatchChapters(
    chapters: ChapterMindmapInput[],
    generationRunId: string | undefined,
) {
    const ordered = [...chapters].sort((left, right) =>
        left.order - right.order || (left.chapterId || '').localeCompare(right.chapterId || ''));
    if (!generationRunId) return ordered;

    const chapterIds = new Set<string>();
    const chapterOrders = new Set<number>();
    for (const chapter of ordered) {
        if (!chapter.chapterId || !CHAPTER_ID_PATTERN.test(chapter.chapterId)
            || !Number.isSafeInteger(chapter.order) || chapter.order < 0 || chapter.order > 999_999
            || chapterOrders.has(chapter.order)
            || chapterIds.has(chapter.chapterId.toLowerCase())) {
            throw new UnrecoverableError('بيانات فصول الخرائط الذهنية غير متطابقة. ابدأ طلب توليد جديد.');
        }
        chapterIds.add(chapter.chapterId.toLowerCase());
        chapterOrders.add(chapter.order);
    }
    return ordered;
}

export function findReusableMindmapUrl(
    mindmapsDirectory: string,
    lessonVideoId: string,
    chapterOrder: number,
    generationRunId: string,
) {
    if (!fs.existsSync(mindmapsDirectory)) return undefined;
    const prefix = mindmapArtifactPrefix(lessonVideoId, chapterOrder, generationRunId);
    const match = fs.readdirSync(mindmapsDirectory)
        .filter(file => {
            if (!file.startsWith(prefix) || file.startsWith(`${prefix}temp_`)) return false;
            if (!file.endsWith('.webp') && !file.endsWith('.png')) return false;
            const stat = fs.lstatSync(path.join(mindmapsDirectory, file));
            return !stat.isSymbolicLink() && stat.isFile() && stat.size > 0;
        })
        .sort()
        .at(-1);
    return match ? `/mindmaps/${match}` : undefined;
}

export interface GenerateMindmapsJobData {
    lessonVideoId: string;
    LessonVideoId?: string;
    outputLanguage?: 'auto' | 'ar' | 'en';
    OutputLanguage?: 'auto' | 'ar' | 'en';
    generationRunId?: string;
    GenerationRunId?: string;
    logicalJobId?: string;
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
    const outputLanguage = parseAiOutputLanguage(job.data.outputLanguage || job.data.OutputLanguage);
    const generationRun = resolveGenerationRun(
        job.data.generationRunId || job.data.GenerationRunId,
        job.id,
        job.timestamp,
    );
    const generationRunId = generationRun.callbackRunId;
    const teacherPhotoUrl = job.data.teacherPhotoUrl || job.data.TeacherPhotoUrl;
    const teacherPhotoUrls = job.data.teacherPhotoUrls || job.data.TeacherPhotoUrls;
    const options = {
        visualStyles: job.data.visualStyles || job.data.VisualStyles || ['editorial-infographic'],
        teacherStyles: job.data.teacherStyles || job.data.TeacherStyles || ['photorealistic'],
        outputLanguage,
        generationRunId: generationRun.artifactRunId,
    };
    const chapterId = job.data.chapterId || job.data.ChapterId;
    const singleChapter = job.data.chapter || job.data.Chapter;
    const isSingleChapter = !!chapterId && !!singleChapter;
    const chapters = isSingleChapter
        ? [singleChapter!]
        : orderedBatchChapters(job.data.chapters || job.data.Chapters || [], generationRunId);

    if (!lessonVideoId) {
        throw new Error('Mindmap job is missing lessonVideoId.');
    }

    const logicalJobId = job.data.logicalJobId || `${lessonVideoId}_mindmaps`;
    const cancellationAliases = [logicalJobId, lessonVideoId, `${lessonVideoId}_mindmaps`];

    console.log(`[Job ${job.id}] Starting ${isSingleChapter ? 'Single-Chapter Regen' : 'Batch'} Mindmaps for VideoId: ${lessonVideoId}`);

    const results: MindmapCallbackResult[] = [];
    let completed = false;
    let terminalFailure = false;
    let completionCallbackAttempted = false;
    let removeCurrentArtifacts = false;

    try {
        const prepStage = 'تحضير شخصية المدرس...';
        await job.updateProgress({ percentage: 10, stage: prepStage });
        await notifyProgress({
            jobId: logicalJobId, generationRunId, percentage: 10, stage: prepStage,
        });
        await throwIfGenerationCancellationRequested(job, cancellationAliases);

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
            throw new UnrecoverableError('لا توجد فصول صالحة لتوليد الخرائط الذهنية. أعد تحليل الفيديو أولًا.');
        }

        let completedCount = 0;

        for (const chapter of chapters) {
            await throwIfGenerationCancellationRequested(job, cancellationAliases);

            // Retries in the same generation run may reuse finished chapters. A new run has a
            // different prefix, so it can never pick up an image produced by an older request.
            let existingUrl: string | undefined = undefined;
            try {
                existingUrl = findReusableMindmapUrl(
                    mindmapsDir,
                    lessonVideoId,
                    chapter.order,
                    generationRun.artifactRunId,
                );
                if (existingUrl) {
                    console.log(`[Job ${job.id}] Reusing existing mindmap for chapter ${chapter.order}: ${existingUrl}`);
                }
            } catch (error) {
                logWarn('mindmap-reuse', 'Could not inspect same-run mindmap artifacts; generation will continue.', {
                    jobId: logicalJobId,
                    errorName: error instanceof Error ? error.name : 'UnknownError',
                });
            }

            const generatedUrl = existingUrl || await generateChapterMindmap(
                chapter,
                lessonVideoId,
                activeTeacherPhotoLocalPaths,
                options,
            );
            results.push({
                ...(chapter.chapterId ? { chapterId: chapter.chapterId } : {}),
                title: chapter.title,
                imageUrl: generatedUrl,
                order: chapter.order,
            });
            completedCount++;
            const progressPct = 10 + Math.floor((completedCount / totalChapters) * 80);
            const chStage = `تم توليد صورة الفصل ${completedCount} من ${totalChapters} (${chapter.title})`;
            await job.updateProgress({ percentage: progressPct, stage: chStage });
            await notifyProgress({
                jobId: logicalJobId, generationRunId, percentage: progressPct, stage: chStage,
            });
        }

        {
            const saveStage = 'جاري حفظ الخرائط في لوحة التحكم...';
            await job.updateProgress({ percentage: 95, stage: saveStage });
            await notifyProgress({
                jobId: logicalJobId, generationRunId, percentage: 95, stage: saveStage,
            });
        }
        await throwIfGenerationCancellationRequested(job, cancellationAliases);

        if (isSingleChapter) {
            // ── Single-chapter regeneration: dedicated webhook ────────────────
            const singleResult = results[0];
            if (singleResult) {
                console.log(`[Job ${job.id}] Pushing single mindmap for chapterId: ${chapterId}...`);
                completionCallbackAttempted = true;
                const webhookResponse = await fetchWithTimeout(
                    `${BACKEND_BASE_URL}/api/v1/internal/callbacks/single-mindmap-completed`,
                    {
                        method: 'POST',
                        timeoutMs: 10_000,
                        maxResponseBytes: 16_384,
                        operation: 'single-mindmap-callback',
                        headers: { 'Content-Type': 'application/json', 'X-Internal-Token': API_KEY },
                        body: JSON.stringify({
                            chapterId,
                            ...(generationRunId ? { generationRunId } : {}),
                            imageUrl: singleResult.imageUrl,
                        })
                    }
                );
                if (!webhookResponse.ok) {
                    throw callbackFailure(webhookResponse.status);
                }
                const callbackAccepted = await readCallbackResponseAcceptance(webhookResponse, generationRunId);
                removeCurrentArtifacts = callbackAccepted === false;
                try {
                    await reconcileMindmapArtifacts(
                        mindmapsDir,
                        lessonVideoId,
                        generationRun.artifactRunId,
                        callbackAccepted,
                        singleChapter!.order,
                    );
                } catch (cleanupError) {
                    logWarn('mindmap-artifact-cleanup', 'Could not reconcile single-chapter mindmap artifacts.', {
                        jobId: logicalJobId,
                        errorName: cleanupError instanceof Error ? cleanupError.name : 'UnknownError',
                    });
                }
            } else {
                console.warn(`[Job ${job.id}] No image generated for chapterId ${chapterId}, skipping webhook.`);
            }
        } else {
            // ── Batch (full video): existing webhook ──────────────────────────
            console.log(`[Job ${job.id}] Pushing ${results.length} batch mindmaps to backend...`);
            completionCallbackAttempted = true;
            const callbackAccepted = await postMindmapResults(lessonVideoId, generationRunId, results);
            removeCurrentArtifacts = callbackAccepted === false;
            try {
                await reconcileMindmapArtifacts(
                    mindmapsDir,
                    lessonVideoId,
                    generationRun.artifactRunId,
                    callbackAccepted,
                );
            } catch (cleanupError) {
                logWarn('mindmap-artifact-cleanup', 'Could not reconcile batch mindmap artifacts.', {
                    jobId: logicalJobId,
                    errorName: cleanupError instanceof Error ? cleanupError.name : 'UnknownError',
                });
            }
        }

        // A successful completion callback is the commit point. Later progress failures
        // cannot safely retry or fail a run whose artifacts may already be referenced.
        completed = true;
        console.log(`[Job ${job.id}] Successfully generated ${results.length} mindmaps.`);
        const doneStage = 'اكتمل توليد الخرائط الذهنية بنجاح.';
        try {
            await job.updateProgress({ percentage: 100, stage: doneStage });
            await notifyProgress({
                jobId: logicalJobId,
                generationRunId,
                percentage: 100,
                stage: doneStage,
                status: 'completed',
            });
        } catch (progressError) {
            logWarn('mindmap-progress', 'Final progress update failed after mindmaps were committed.', {
                jobId: logicalJobId,
                errorName: progressError instanceof Error ? progressError.name : 'UnknownError',
            });
        }
        return { success: true, mindmapsGenerated: results.length };

    } catch (error) {
        const retryable = error instanceof WorkerExternalError ? error.retryable : true;
        terminalFailure = error instanceof UnrecoverableError || !retryable;
        logWarn('mindmap-generation', 'Mindmap generation attempt failed.', {
            jobId: logicalJobId,
            errorName: error instanceof Error ? error.name : 'UnknownError',
            retryable,
        });
        if (error instanceof WorkerExternalError && !error.retryable) {
            throw new UnrecoverableError(error.remediation);
        }
        throw error;
    } finally {
        const terminalWithoutCallbackAttempt = !completed
            && !completionCallbackAttempted
            && (terminalFailure || isFinalJobAttempt(job));
        if (removeCurrentArtifacts || terminalWithoutCallbackAttempt) {
            try {
                await cleanupCurrentMindmapArtifacts(
                    sharedMindmapsRoot,
                    lessonVideoId,
                    generationRun.artifactRunId,
                );
            } catch (cleanupError) {
                logWarn('mindmap-artifact-cleanup', 'Could not remove terminal run mindmap artifacts.', {
                    jobId: logicalJobId,
                    errorName: cleanupError instanceof Error ? cleanupError.name : 'UnknownError',
                });
            }
        }
    }
}

export default generateMindmapsProcessor;
