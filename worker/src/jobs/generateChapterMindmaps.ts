import { Job } from 'bullmq';
import path from 'path';
import { fileURLToPath } from 'url';
import { generateChapterMindmap } from '../services/geminiService.js';
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
    } catch { /* best-effort */ }
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
    const chapterId = job.data.chapterId || job.data.ChapterId;
    const singleChapter = job.data.chapter || job.data.Chapter;
    const isSingleChapter = !!chapterId && !!singleChapter;
    const chapters = isSingleChapter ? [singleChapter!] : (job.data.chapters || job.data.Chapters || []);

    if (!lessonVideoId) {
        throw new Error('Mindmap job is missing lessonVideoId.');
    }

    console.log(`[Job ${job.id}] Starting ${isSingleChapter ? 'Single-Chapter Regen' : 'Batch'} Mindmaps for VideoId: ${lessonVideoId}`);

    try {
        const prepStage = 'تحضير شخصية المدرس...';
        await job.updateProgress({ percentage: 10, stage: prepStage });
        await notifyProgress(`${lessonVideoId}_mindmaps`, 10, prepStage);
        await throwIfCancellationRequested(job);

        // Prepare local path for teacherPhotoUrl if it exists
        let activeTeacherPhotoLocalPath: string | undefined = undefined;
        if (teacherPhotoUrl) {
            const relativeToWwwroot = teacherPhotoUrl.startsWith('/')
                ? teacherPhotoUrl.substring(1)
                : teacherPhotoUrl;
            activeTeacherPhotoLocalPath = path.join(
                workerRoot,
                '../backend/src/NaderGorge.API/wwwroot',
                relativeToWwwroot
            );
        }

        const totalChapters = chapters.length;
        if (totalChapters === 0) {
            const noChStage = 'لا توجد فصول لتوليد الصور لها.';
            await job.updateProgress({ percentage: 100, stage: noChStage });
            await notifyProgress(`${lessonVideoId}_mindmaps`, 100, noChStage, 'completed');
            return { success: true, mindmapsGenerated: 0 };
        }

        const results: Array<{ title: string; imageUrl: string }> = [];
        let completedCount = 0;

        for (const chapter of chapters) {
            await throwIfCancellationRequested(job);
            const generatedUrl = await generateChapterMindmap(chapter, lessonVideoId, activeTeacherPhotoLocalPath);
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
                const webhookResponse = await fetch(
                    `${BACKEND_BASE_URL}/api/v1/internal/callbacks/single-mindmap-completed`,
                    {
                        method: 'POST',
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
            const webhookResponse = await fetch(
                `${BACKEND_BASE_URL}/api/v1/internal/callbacks/mindmaps-completed`,
                {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'X-Internal-Token': API_KEY },
                    body: JSON.stringify({ videoId: lessonVideoId, mindmaps: results })
                }
            );
            if (!webhookResponse.ok) {
                const errBody = await webhookResponse.text();
                throw new Error(`Webhook failed ${webhookResponse.status}: ${errBody}`);
            }
        }

        console.log(`[Job ${job.id}] Successfully generated ${results.length} mindmaps.`);
        const doneStage = 'اكتمل توليد الخرائط الذهنية بنجاح.';
        await job.updateProgress({ percentage: 100, stage: doneStage });
        await notifyProgress(`${lessonVideoId}_mindmaps`, 100, doneStage, 'completed');
        return { success: true, mindmapsGenerated: results.length };

    } catch (error) {
        console.error(`[Job ${job.id}] Failed generating mindmaps:`, error);
        throw error;
    }
}

export default generateMindmapsProcessor;
