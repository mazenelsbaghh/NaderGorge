import { Job, UnrecoverableError } from 'bullmq';
import fs from 'fs';
import { extractAudioFromVideo } from '../utils/audioExtractor.js';
import { generateVideoChapters, transcribePublicYouTubeVideo, transcribeVideoAudio } from '../services/geminiService.js';
import type { VideoAIResult } from '../services/geminiService.js';
import { throwIfCancellationRequested } from '../cancellation.js';
import { fetchWithTimeout, WorkerExternalError } from '../services/workerFetch.js';
import { atomicWriteFileSync, sharedSubtitlesRoot } from '../config/storage.js';
import { createVideoAnalysisCheckpoint } from '../services/aiVideoCheckpoint.js';
import { isFinalJobAttempt, removeJobTempFile } from '../utils/jobTempFiles.js';
import { logWarn } from '../logging.js';
import { normalizePublicYouTubeUrl } from '../utils/youtubeSource.js';
import { GeminiDeveloperApiError } from '../services/aiProvider.js';

const BACKEND_BASE_URL = process.env.BACKEND_API_URL || 'http://localhost:5245';
const API_KEY = process.env.API_CALLBACK_SECRET || process.env.AI_CALLBACK_SECRET || '';

/** Push progress to backend → SignalR → admin frontend in real time */
async function notifyProgress(jobId: string, percentage: number, stage: string, status = 'active') {
    try {
        await fetchWithTimeout(`${BACKEND_BASE_URL}/api/v1/internal/callbacks/ai-progress`, {
            method: 'POST',
            timeoutMs: 10_000,
            operation: 'ai-progress',
            headers: { 'Content-Type': 'application/json', 'X-Internal-Token': API_KEY },
            body: JSON.stringify({ jobId, progress: percentage, status, message: stage }),
        });
    } catch (error) {
        logWarn('ai-progress', 'Progress callback failed; pipeline will continue.', { jobId, error });
    }
}

export interface AnalyzeVideoJobData {
    lessonVideoId: string;
    sourceUrl: string;
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
    
    console.log(`[Job ${job.id}] Starting analysis for LessonVideoId: ${lessonVideoId}`);

    let audioPath = job.data.audioPath || '';
    let result: VideoAIResult | null = null;
    let isSuccess = false;
    let isTerminalFailure = false;
    const checkpoint = createVideoAnalysisCheckpoint(lessonVideoId, sourceUrl);
    const publicYoutubeUrl = normalizePublicYouTubeUrl(sourceUrl);

    try {
        await throwIfCancellationRequested(job);

        let srtContent = checkpoint.transcription();
        let chapters = checkpoint.chapters();

        if (!srtContent) {
            const prepareStage = publicYoutubeUrl
                ? 'جاري تجهيز فيديو YouTube العام للتحليل المباشر...'
                : 'جاري استخراج وتحضير الصوت من الفيديو...';
            await job.updateProgress({ percentage: 10, stage: prepareStage });
            await notifyProgress(lessonVideoId, 10, prepareStage);
            await throwIfCancellationRequested(job);

            if (!publicYoutubeUrl && (!audioPath || !fs.existsSync(audioPath))) {
                audioPath = await extractAudioFromVideo(sourceUrl, lessonVideoId);
                await job.updateData({ ...job.data, audioPath });
            }

            const stage = publicYoutubeUrl
                ? 'جاري تحويل فيديو YouTube إلى ترجمة مكتوبة مباشرة...'
                : 'جاري تحويل صوت المحاضرة إلى ترجمة مكتوبة...';
            await job.updateProgress({ percentage: 40, stage });
            await notifyProgress(lessonVideoId, 40, stage);
            await throwIfCancellationRequested(job);
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
            await notifyProgress(lessonVideoId, 65, stage);
            await throwIfCancellationRequested(job);
            chapters = await generateVideoChapters(srtContent);
            checkpoint.saveChapters(chapters);
        } else {
            console.log(`[Job ${job.id}] Reusing completed chapters checkpoint.`);
        }

        await throwIfCancellationRequested(job);
        result = { srtContent, chapters };

        // Save SRT file to configured shared storage.
        {
            const stage = 'جاري بناء هيكل الفصول وإنشاء الترجمة...';
            await job.updateProgress({ percentage: 85, stage });
            await notifyProgress(lessonVideoId, 85, stage);
        }
        await throwIfCancellationRequested(job);
        const srtFileName = `${lessonVideoId}.srt`;
        atomicWriteFileSync(sharedSubtitlesRoot, srtFileName, result.srtContent, 'utf8');
        
        const subtitleBaseUrl = (process.env.PUBLIC_SUBTITLE_BASE_URL || '/subtitles').replace(/\/$/, '');
        const subtitleUrl = `${subtitleBaseUrl}/${srtFileName}`;
        
        // Step 3: Webhook Callback to .NET API
        {
            const stage = 'جاري حفظ الفصول والخرائط في واجهة النظام...';
            await job.updateProgress({ percentage: 95, stage });
            await notifyProgress(lessonVideoId, 95, stage);
        }
        await throwIfCancellationRequested(job);
        console.log(`[Job ${job.id}] Pushing results to backend via Webhook...`);
        
        const webhookResponse = await fetchWithTimeout(`${BACKEND_BASE_URL}/api/v1/internal/callbacks/ai-analysis-completed`, {
            method: 'POST',
            timeoutMs: 10_000,
            operation: 'ai-analysis-callback',
            headers: {
                'Content-Type': 'application/json',
                'X-Internal-Token': API_KEY
            },
            body: JSON.stringify({
                videoId: lessonVideoId,
                subtitleUrl: subtitleUrl,
                chapters: result.chapters,
                jobId: job.id
            })
        });

        if (!webhookResponse.ok) {
            const errBody = await webhookResponse.text();
            throw new Error(`Webhook failed with status ${webhookResponse.status}: ${errBody}`);
        }

        console.log(`[Job ${job.id}] Successfully processed video ${lessonVideoId}`);
        const doneStage = 'اكتملت المعالجة بنجاح مئة بالمئة.';
        await job.updateProgress({ percentage: 100, stage: doneStage });
        await notifyProgress(lessonVideoId, 100, doneStage, 'completed');
        
        isSuccess = true;
        checkpoint.clear();
        return { success: true, chaptersProcessed: result.chapters.length };
        
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
        // Keep audio only while BullMQ can retry this attempt; terminal jobs must not leak disk space.
        if (isSuccess || isTerminalFailure || isFinalJobAttempt(job)) {
            removeJobTempFile(audioPath, job.id);
        }
    }
}
