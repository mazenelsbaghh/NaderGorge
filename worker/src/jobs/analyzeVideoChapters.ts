import { Job, UnrecoverableError } from 'bullmq';
import fs from 'fs';
import { extractAudioFromVideo } from '../utils/audioExtractor.js';
import { assertChapterOutputLanguage, generateVideoChapters, transcribePublicYouTubeVideo, transcribeVideoAudio } from '../services/geminiService.js';
import type { VideoAIResult } from '../services/geminiService.js';
import { throwIfGenerationCancellationRequested } from './generationCancellation.js';
import { fetchWithTimeout, WorkerExternalError } from '../services/workerFetch.js';
import { atomicWriteFileSync, sharedSubtitlesRoot } from '../config/storage.js';
import { createVideoAnalysisCheckpoint } from '../services/aiVideoCheckpoint.js';
import { isFinalJobAttempt, removeJobTempFile } from '../utils/jobTempFiles.js';
import { logWarn } from '../logging.js';
import { normalizePublicYouTubeUrl } from '../utils/youtubeSource.js';
import { GeminiDeveloperApiError } from '../services/aiProvider.js';
import { parseAiOutputLanguage, resolveGenerationRun } from '../services/aiGenerationContract.js';
import {
    readCallbackResponseAcceptance,
    reconcileAnalysisArtifacts,
} from '../services/generationArtifactCleanup.js';

const BACKEND_BASE_URL = process.env.BACKEND_API_URL || 'http://localhost:5245';
const API_KEY = process.env.API_CALLBACK_SECRET || process.env.AI_CALLBACK_SECRET || '';

/** Push progress to backend → SignalR → admin frontend in real time */
interface AnalysisProgressUpdate {
    jobId: string,
    generationRunId: string | undefined,
    percentage: number,
    stage: string,
    status?: string,
}

async function notifyProgress(update: AnalysisProgressUpdate) {
    const { jobId, generationRunId, percentage, stage, status = 'active' } = update;
    try {
        await fetchWithTimeout(`${BACKEND_BASE_URL}/api/v1/internal/callbacks/ai-progress`, {
            method: 'POST',
            timeoutMs: 10_000,
            operation: 'ai-progress',
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
        logWarn('ai-progress', 'Progress callback failed; pipeline will continue.', {
            jobId,
            errorName: error instanceof Error ? error.name : 'UnknownError',
        });
    }
}

export interface AnalyzeVideoJobData {
    lessonVideoId: string;
    sourceUrl: string;
    outputLanguage?: 'auto' | 'ar' | 'en';
    generationRunId?: string;
    logicalJobId?: string;
    audioPath?: string;
    aiRawResponse?: any;
    srtContent?: string;
    subtitleUrl?: string;
    teacherPhotoUrl?: string;
    teacherPhotoUrls?: string[];
}

function safeAnalysisFailure(error: unknown) {
    if (error instanceof WorkerExternalError) return error;
    if (error instanceof GeminiDeveloperApiError) {
        const retryable = error.category === 'quota-exhausted'
            || error.category === 'provider-timeout'
            || error.category === 'provider';
        return new WorkerExternalError(
            retryable ? 'provider' : 'rejected',
            retryable,
            retryable
                ? 'تعذر إكمال تحليل الفيديو مؤقتًا. ستتم إعادة المحاولة تلقائيًا.'
                : 'رفض مزود الذكاء الاصطناعي طلب تحليل الفيديو. راجع إتاحة الفيديو وإعدادات المزود.',
        );
    }
    return new WorkerExternalError(
        'implementation',
        false,
        'حدث خطأ داخلي أثناء تحليل الفيديو. تواصل مع الدعم قبل إعادة المحاولة.',
    );
}

/**
 * The BullMQ Job Processor for extracting audio and sending it to Gemini.
 */
export default async function analyzeVideoProcessor(job: Job<AnalyzeVideoJobData>) {
    const { lessonVideoId, sourceUrl } = job.data;
    const outputLanguage = parseAiOutputLanguage(job.data.outputLanguage);
    const generationRun = resolveGenerationRun(job.data.generationRunId, job.id, job.timestamp);
    const generationRunId = generationRun.callbackRunId;
    const logicalJobId = job.data.logicalJobId || lessonVideoId;
    const cancellationAliases = [logicalJobId, lessonVideoId];
    
    console.log(`[Job ${job.id}] Starting analysis for LessonVideoId: ${lessonVideoId}`);

    let audioPath = job.data.audioPath || '';
    let analysisResult: VideoAIResult | null = null;
    let isSuccess = false;
    let isTerminalFailure = false;
    let srtWritten = false;
    let completionCallbackAttempted = false;
    let removeCurrentSrt = false;
    const checkpoint = createVideoAnalysisCheckpoint(
        lessonVideoId,
        sourceUrl,
        outputLanguage,
        generationRun.artifactRunId,
    );
    const publicYoutubeUrl = normalizePublicYouTubeUrl(sourceUrl);

    try {
        await throwIfGenerationCancellationRequested(job, cancellationAliases);

        let srtContent = checkpoint.transcription();
        let chapters = checkpoint.chapters();

        if (!srtContent) {
            const prepareStage = publicYoutubeUrl
                ? 'جاري تجهيز فيديو YouTube العام للتحليل المباشر...'
                : 'جاري استخراج وتحضير الصوت من الفيديو...';
            await job.updateProgress({ percentage: 10, stage: prepareStage });
            await notifyProgress({ jobId: logicalJobId, generationRunId, percentage: 10, stage: prepareStage });
            await throwIfGenerationCancellationRequested(job, cancellationAliases);

            if (!publicYoutubeUrl && (!audioPath || !fs.existsSync(audioPath))) {
                audioPath = await extractAudioFromVideo(sourceUrl, lessonVideoId);
                await job.updateData({ ...job.data, audioPath });
            }

            const stage = publicYoutubeUrl
                ? 'جاري تحويل فيديو YouTube إلى ترجمة مكتوبة مباشرة...'
                : 'جاري تحويل صوت المحاضرة إلى ترجمة مكتوبة...';
            await job.updateProgress({ percentage: 40, stage });
            await notifyProgress({ jobId: logicalJobId, generationRunId, percentage: 40, stage });
            await throwIfGenerationCancellationRequested(job, cancellationAliases);
            srtContent = publicYoutubeUrl
                ? await transcribePublicYouTubeVideo(publicYoutubeUrl)
                : await transcribeVideoAudio(audioPath);
            checkpoint.saveTranscription(srtContent);
        } else {
            console.log(`[Job ${job.id}] Reusing completed transcription checkpoint; media extraction is not required.`);
        }

        if (!chapters) {
            const stage = 'جاري تقسيم المحاضرة إلى فصول وكتابة الملخصات...';
            await job.updateProgress({ percentage: 65, stage });
            await notifyProgress({ jobId: logicalJobId, generationRunId, percentage: 65, stage });
            await throwIfGenerationCancellationRequested(job, cancellationAliases);
            chapters = await generateVideoChapters(srtContent, outputLanguage);
            checkpoint.saveChapters(chapters);
        } else {
            assertChapterOutputLanguage(chapters, outputLanguage);
            console.log(`[Job ${job.id}] Reusing completed chapters checkpoint.`);
        }

        await throwIfGenerationCancellationRequested(job, cancellationAliases);
        analysisResult = { srtContent, chapters };

        // Save SRT file to configured shared storage.
        {
            const stage = 'جاري بناء هيكل الفصول وإنشاء الترجمة...';
            await job.updateProgress({ percentage: 85, stage });
            await notifyProgress({ jobId: logicalJobId, generationRunId, percentage: 85, stage });
        }
        await throwIfGenerationCancellationRequested(job, cancellationAliases);
        const srtFileName = `${lessonVideoId}_run_${generationRun.artifactRunId}.srt`;
        atomicWriteFileSync(sharedSubtitlesRoot, srtFileName, analysisResult.srtContent, 'utf8');
        srtWritten = true;
        
        const subtitleBaseUrl = (process.env.PUBLIC_SUBTITLE_BASE_URL || '/subtitles').replace(/\/$/, '');
        const subtitleUrl = `${subtitleBaseUrl}/${srtFileName}`;
        
        // Step 3: Webhook Callback to .NET API
        {
            const stage = 'جاري حفظ الفصول والخرائط في واجهة النظام...';
            await job.updateProgress({ percentage: 95, stage });
            await notifyProgress({ jobId: logicalJobId, generationRunId, percentage: 95, stage });
        }
        await throwIfGenerationCancellationRequested(job, cancellationAliases);
        console.log(`[Job ${job.id}] Pushing results to backend via Webhook...`);
        completionCallbackAttempted = true;
        const webhookResponse = await fetchWithTimeout(`${BACKEND_BASE_URL}/api/v1/internal/callbacks/ai-analysis-completed`, {
            method: 'POST',
            timeoutMs: 10_000,
            maxResponseBytes: 16_384,
            operation: 'ai-analysis-callback',
            headers: {
                'Content-Type': 'application/json',
                'X-Internal-Token': API_KEY
            },
            body: JSON.stringify({
                videoId: lessonVideoId,
                subtitleUrl: subtitleUrl,
                chapters: analysisResult.chapters,
                jobId: logicalJobId,
                ...(generationRunId ? { generationRunId } : {}),
            })
        });

        if (!webhookResponse.ok) {
            const retryable = webhookResponse.status === 408
                || webhookResponse.status === 429
                || webhookResponse.status >= 500;
            throw new WorkerExternalError(
                retryable ? 'provider' : 'rejected',
                retryable,
                retryable
                    ? 'تعذر حفظ نتيجة تحليل الفيديو مؤقتًا. ستتم إعادة المحاولة تلقائيًا.'
                    : 'رفضت المنصة حفظ نتيجة تحليل الفيديو. ابدأ طلب تحليل جديد من لوحة التحكم.',
            );
        }

        const callbackAccepted = await readCallbackResponseAcceptance(webhookResponse, generationRunId);
        removeCurrentSrt = callbackAccepted === false;
        try {
            await reconcileAnalysisArtifacts(
                sharedSubtitlesRoot,
                lessonVideoId,
                generationRun.artifactRunId,
                callbackAccepted,
            );
        } catch (cleanupError) {
            logWarn('ai-analysis-artifact-cleanup', 'Could not reconcile run-scoped subtitle artifacts.', {
                jobId: logicalJobId,
                errorName: cleanupError instanceof Error ? cleanupError.name : 'UnknownError',
            });
        }

        // A successful completion callback is the commit point. Progress reporting after it
        // must never turn an accepted generation into a failed BullMQ job.
        isSuccess = true;
        console.log(`[Job ${job.id}] Successfully processed video ${lessonVideoId}`);
        const doneStage = 'اكتملت المعالجة بنجاح مئة بالمئة.';
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
            logWarn('ai-progress', 'Final progress update failed after analysis was committed.', {
                jobId: logicalJobId,
                errorName: progressError instanceof Error ? progressError.name : 'UnknownError',
            });
        }

        return { success: true, chaptersProcessed: analysisResult.chapters.length };
        
    } catch (error) {
        if (error instanceof UnrecoverableError) {
            isTerminalFailure = true;
            throw error;
        }
        const safeFailure = safeAnalysisFailure(error);
        console.error(`[Job ${job.id}] Video analysis failed.`, {
            category: safeFailure.category,
            retryable: safeFailure.retryable,
        });
        if (!safeFailure.retryable) {
            isTerminalFailure = true;
            throw new UnrecoverableError(safeFailure.remediation);
        }
        throw safeFailure;
    } finally {
        const terminalBeforeCallbackAttempt = srtWritten
            && !completionCallbackAttempted
            && (isTerminalFailure || isFinalJobAttempt(job));
        if (removeCurrentSrt || terminalBeforeCallbackAttempt) {
            try {
                await reconcileAnalysisArtifacts(
                    sharedSubtitlesRoot,
                    lessonVideoId,
                    generationRun.artifactRunId,
                    false,
                );
            } catch (cleanupError) {
                logWarn('ai-analysis-artifact-cleanup', 'Could not remove a terminal run subtitle artifact.', {
                    jobId: logicalJobId,
                    errorName: cleanupError instanceof Error ? cleanupError.name : 'UnknownError',
                });
            }
        }
        // Keep audio only while BullMQ can retry this attempt; terminal jobs must not leak disk space.
        if (isSuccess || isTerminalFailure || isFinalJobAttempt(job)) {
            checkpoint.clear();
            removeJobTempFile(audioPath, job.id);
        }
    }
}
