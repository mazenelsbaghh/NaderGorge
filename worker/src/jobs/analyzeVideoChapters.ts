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
            await job.updateProgress({ percentage: 10, stage: 'جاري استخراج وتحضير الصوت من الفيديو...' });
            await throwIfCancellationRequested(job);
            audioPath = await extractAudioFromVideo(sourceUrl, lessonVideoId);
            await job.updateData({ ...job.data, audioPath });
        } else {
            console.log(`[Job ${job.id}] Found existing extracted audio file. Skipping extraction stage.`);
        }
        
        // Step 2: Upload to Gemini & Execute Flash 2.5 Prompt
        await job.updateProgress({ percentage: 40, stage: 'الذكاء الاصطناعي يقوم بتحليل وتلخيص المحتوى (قد يستغرق دقائق)...' });
        await throwIfCancellationRequested(job);
        console.log(`[Job ${job.id}] Starting Gemini processing...`);
        result = await analyzeVideoChapters(audioPath);

        // Save SRT file to configured shared storage.
        await job.updateProgress({ percentage: 85, stage: 'جاري بناء هيكل الفصول وإنشاء الترجمة...' });
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
        await job.updateProgress({ percentage: 95, stage: 'جاري حفظ الفصول والخرائط في واجهة النظام...' });
        await throwIfCancellationRequested(job);
        console.log(`[Job ${job.id}] Pushing results to backend via Webhook...`);
        
        const backendBaseUrl = process.env.BACKEND_API_URL || 'http://localhost:5245';
        const apiKey = process.env.API_CALLBACK_SECRET;
        
        const webhookResponse = await fetch(`${backendBaseUrl}/api/v1/internal/callbacks/ai-analysis-completed`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Internal-Token': apiKey || ''
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
        await job.updateProgress({ percentage: 100, stage: 'اكتملت المعالجة بنجاح مئة بالمئة.' });
        
        isSuccess = true;
        return { success: true, chaptersProcessed: result.chapters.length };
        
    } catch (error) {
        console.error(`[Job ${job.id}] Failed processing video:`, error);
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
