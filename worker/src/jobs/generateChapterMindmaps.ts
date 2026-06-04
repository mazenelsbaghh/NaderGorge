import { Job } from 'bullmq';
import path from 'path';
import { fileURLToPath } from 'url';
import { generateChapterMindmap } from '../services/geminiService.js';

// Resolve worker root reliably regardless of process.cwd()
const __filename = fileURLToPath(import.meta.url);
const __dirname  = path.dirname(__filename);
const workerRoot = path.resolve(__dirname, '../../');


export interface GenerateMindmapsJobData {
    lessonVideoId: string;
    teacherPhotoUrl?: string;
    // Batch mode
    chapters?: Array<{
        title: string;
        summaryText: string;
        order: number;
    }>;
    // Single-chapter regeneration mode
    chapterId?: string;
    chapter?: {
        title: string;
        summaryText: string;
        order: number;
    };
}

/**
 * The BullMQ Job Processor for generating mindmaps.
 * Supports two modes:
 *   - Batch: { lessonVideoId, chapters[], teacherPhotoUrl? }
 *   - Single: { lessonVideoId, chapterId, chapter, teacherPhotoUrl? }
 */
export default async function generateMindmapsProcessor(job: Job<any>) {
    const lessonVideoId = job.data.lessonVideoId || job.data.LessonVideoId;
    const teacherPhotoUrl = job.data.teacherPhotoUrl || job.data.TeacherPhotoUrl;
    const chapterId = job.data.chapterId || job.data.ChapterId;
    const singleChapter = job.data.chapter || job.data.Chapter;
    const isSingleChapter = !!chapterId && !!singleChapter;
    const chapters = isSingleChapter ? [singleChapter!] : (job.data.chapters || job.data.Chapters || []);

    console.log(`[Job ${job.id}] Starting ${isSingleChapter ? 'Single-Chapter Regen' : 'Batch'} Mindmaps for VideoId: ${lessonVideoId}`);

    try {
        await job.updateProgress({ percentage: 10, stage: 'تحضير شخصية المدرس...' });

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
            await job.updateProgress({ percentage: 100, stage: 'لا توجد فصول لتوليد الصور لها.' });
            return { success: true, mindmapsGenerated: 0 };
        }

        const results: Array<{ title: string; imageUrl: string }> = [];
        let completedCount = 0;

        for (const chapter of chapters) {
            try {
                const generatedUrl = await generateChapterMindmap(chapter, lessonVideoId, activeTeacherPhotoLocalPath);
                if (generatedUrl) {
                    results.push({ title: chapter.title, imageUrl: generatedUrl });
                }
                completedCount++;
                const progressPct = 10 + Math.floor((completedCount / totalChapters) * 80);
                await job.updateProgress({
                    percentage: progressPct,
                    stage: `جاري توليد صورة الفصل ${completedCount} من ${totalChapters} (${chapter.title})...`
                });
            } catch (e) {
                console.error(`[Job ${job.id}] Failed to generate mind map for ${chapter.title}`, e);
                // Don't abort the whole job if one chapter fails
            }
        }

        await job.updateProgress({ percentage: 95, stage: 'جاري حفظ الخرائط في لوحة التحكم...' });

        const backendBaseUrl = process.env.BACKEND_API_URL || 'http://localhost:5245';
        const apiKey = process.env.API_CALLBACK_SECRET;

        if (isSingleChapter) {
            // ── Single-chapter regeneration: dedicated webhook ────────────────
            const singleResult = results[0];
            if (singleResult) {
                console.log(`[Job ${job.id}] Pushing single mindmap for chapterId: ${chapterId}...`);
                const webhookResponse = await fetch(
                    `${backendBaseUrl}/api/v1/internal/callbacks/single-mindmap-completed`,
                    {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'X-Internal-Token': apiKey || '' },
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
                `${backendBaseUrl}/api/v1/internal/callbacks/mindmaps-completed`,
                {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'X-Internal-Token': apiKey || '' },
                    body: JSON.stringify({ videoId: lessonVideoId, mindmaps: results })
                }
            );
            if (!webhookResponse.ok) {
                const errBody = await webhookResponse.text();
                throw new Error(`Webhook failed ${webhookResponse.status}: ${errBody}`);
            }
        }

        console.log(`[Job ${job.id}] Successfully generated ${results.length} mindmaps.`);
        await job.updateProgress({ percentage: 100, stage: 'اكتمل توليد الخرائط الذهنية بنجاح.' });
        return { success: true, mindmapsGenerated: results.length };

    } catch (error) {
        console.error(`[Job ${job.id}] Failed generating mindmaps:`, error);
        throw error;
    }
}
