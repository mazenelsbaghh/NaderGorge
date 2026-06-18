import { Job } from 'bullmq';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { extractAudioFromVideo } from '../utils/audioExtractor.js';
import { analyzeVideoChapters } from '../services/geminiService.js';
import type { VideoAIResult } from '../services/geminiService.js';
import { throwIfCancellationRequested } from '../cancellation.js';

// Resolve worker root reliably regardless of process.cwd()
const __filename = fileURLToPath(import.meta.url);
const __dirname  = path.dirname(__filename);
const workerRoot = path.resolve(__dirname, '../../');

const BACKEND_BASE_URL = process.env.BACKEND_API_URL || 'http://localhost:5245';
const API_KEY = process.env.API_CALLBACK_SECRET || process.env.AI_CALLBACK_SECRET || '';

/** Push progress to backend → SignalR → admin frontend in real time */
async function notifyProgress(jobId: string, percentage: number, stage: string, status = 'active') {
    try {
        await fetch(`${BACKEND_BASE_URL}/api/v1/internal/callbacks/ai-progress`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-Internal-Token': API_KEY },
            body: JSON.stringify({ jobId, progress: percentage, status, message: stage }),
        });
    } catch { /* best-effort, don't block the pipeline */ }
}

export interface AnalyzeVideoJobData {
    lessonVideoId: string;
    sourceUrl: string;
    audioPath?: string;
    aiRawResponse?: any;
    srtContent?: string;
    subtitleUrl?: string;
    teacherPhotoUrl?: string;
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

    try {
        await throwIfCancellationRequested(job);

        // Step 1: Extract Audio via FFmpeg (saves locally to .tmp directory)
        if (!audioPath || !fs.existsSync(audioPath)) {
            const stage = 'جاري استخراج وتحضير الصوت من الفيديو...';
            await job.updateProgress({ percentage: 10, stage });
            await notifyProgress(lessonVideoId, 10, stage);
            await throwIfCancellationRequested(job);
            audioPath = await extractAudioFromVideo(sourceUrl, lessonVideoId);
            await job.updateData({ ...job.data, audioPath });
        } else {
            console.log(`[Job ${job.id}] Found existing extracted audio file. Skipping extraction stage.`);
        }
        
        // Step 2: Upload to Gemini & Execute Flash 2.5 Prompt
        {
            const stage = 'الذكاء الاصطناعي يقوم بتحليل وتلخيص المحتوى (قد يستغرق دقائق)...';
            await job.updateProgress({ percentage: 40, stage });
            await notifyProgress(lessonVideoId, 40, stage);
        }
        await throwIfCancellationRequested(job);
        console.log(`[Job ${job.id}] Starting Gemini processing...`);
        result = await analyzeVideoChapters(audioPath);

        // Save SRT file to configured shared storage.
        {
            const stage = 'جاري بناء هيكل الفصول وإنشاء الترجمة...';
            await job.updateProgress({ percentage: 85, stage });
            await notifyProgress(lessonVideoId, 85, stage);
        }
        await throwIfCancellationRequested(job);
        const srtDir = process.env.SUBTITLE_STORAGE_PATH || path.join(workerRoot, '.tmp/subtitles');
        if (!fs.existsSync(srtDir)) {
            fs.mkdirSync(srtDir, { recursive: true });
        }
        
        const srtFileName = `${lessonVideoId}.srt`;
        const srtFilePath = path.join(srtDir, srtFileName);
        fs.writeFileSync(srtFilePath, result.srtContent, 'utf-8');
        
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
        
        const webhookResponse = await fetch(`${BACKEND_BASE_URL}/api/v1/internal/callbacks/ai-analysis-completed`, {
            method: 'POST',
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
        return { success: true, chaptersProcessed: result.chapters.length };
        
    } catch (error) {
        console.error(`[Job ${job.id}] Failed processing video:`, error);
        await notifyProgress(lessonVideoId, 0, String(error), 'failed');
        throw error;
    } finally {
        // Cleanup temp audio file ONLY when the pipeline is completely successful.
        // If it failed midway, we keep the audio file so we can jump straight into the AI next time.
        if (isSuccess && audioPath && fs.existsSync(audioPath)) {
            try {
               fs.unlinkSync(audioPath);
            } catch(e) { /* ignore */ }
        }
    }
}
