import { GoogleGenAI, Type } from '@google/genai';
import { Agent, setGlobalDispatcher } from 'undici';
import fs from 'fs';
import path from 'path';

// Node.js native fetch (undici) has a strict 5-minute headers timeout by default.
// Gemini analyzing a 1-hour audio file can easily take 7-10 minutes. 
// We override the global dispatcher to allow up to 60 minutes.
setGlobalDispatcher(new Agent({
    connectTimeout: 60 * 60 * 1000,
    headersTimeout: 60 * 60 * 1000,
    bodyTimeout: 60 * 60 * 1000,
}));

// Initialize the API using the new genai SDK.
// It automatically picks up GEMINI_API_KEY from environment.
const ai = new GoogleGenAI({
    httpOptions: { timeout: 60 * 60 * 1000 } // 60 mins fallback timeout for genai request
});

export interface VideoAIResult {
    srtContent: string;
    chapters: Array<{
        title: string;
        startTime: number;
        endTime: number;
        summaryText: string;
        order: number;
    }>;
}

/**
 * Uploads an audio file via the File API, then runs TWO sequential Gemini calls:
 *   Call 1 → SRT transcription only  (plain text, no JSON overhead)
 *   Call 2 → Chapter analysis only   (small JSON, fits comfortably in token budget)
 *
 * WHY TWO CALLS?
 * A long lesson (60-100 min) produces 1000-1200 SRT subtitle blocks. Combined with
 * chapter JSON and field names, the single-call response exceeded Gemini's output
 * token limit, causing the stream to be cut off mid-JSON and failing to parse.
 * Splitting gives each task its own full token budget.
 */
export async function analyzeVideoChapters(audioFilePath: string): Promise<VideoAIResult> {
    console.log(`[Gemini] Uploading prepared audio file to File API.`);

    // ── 1. Upload once — reuse URI for both calls ────────────────────────────
    const uploadResult = await ai.files.upload({
        file: audioFilePath,
        config: { mimeType: 'audio/mp3', displayName: 'LessonAudioTrack' }
    });
    console.log(`[Gemini] Upload complete.`);
    await new Promise(resolve => setTimeout(resolve, 5000)); // brief processing wait

    const fileRef = { fileUri: uploadResult.uri!, mimeType: uploadResult.mimeType || 'audio/mp3' };

    // ── 2. CALL A: SRT Transcription (plain text output — no JSON wrapper) ───
    console.log(`[Gemini] CALL A — Starting SRT transcription...`);
    const srtPrompt = `You are an expert Arabic transcription AI for an Egyptian educational platform.

Listen to the attached audio file and produce a COMPLETE, verbatim Arabic transcription in standard SRT subtitle format.

RULES:
- Output ONLY the raw SRT content. No JSON. No markdown fences. No extra commentary.
- Every spoken word must appear in the subtitles — do not skip, summarize, or omit anything.
- Each subtitle block must follow EXACTLY this format:
  [number]
  [HH:MM:SS,mmm --> HH:MM:SS,mmm]
  [Arabic text]
  [blank line]
- Timestamps must be precise to the millisecond.
- Text direction is Right-to-Left Arabic — preserve it exactly.
- Do NOT add any text before block 1 or after the last block.`;

    const srtResponse = await ai.models.generateContent({
        model: 'gemini-2.5-flash',
        contents: [{
            role: 'user',
            parts: [
                { fileData: fileRef },
                { text: srtPrompt }
            ]
        }],
        config: {
            // Plain text so every output token is pure SRT content —
            // no JSON structural overhead eating into the budget.
            responseMimeType: 'text/plain',
        }
    });

    const srtContent = (srtResponse.text || '').trim();
    if (!srtContent) {
        throw new Error('[Gemini] CALL A returned empty SRT content.');
    }
    console.log(`[Gemini] CALL A complete. SRT length: ${srtContent.length} chars.`);

    // ── 3. CALL B: Chapter Analysis (compact structured JSON) ────────────────
    console.log(`[Gemini] CALL B — Starting chapter analysis...`);
    const chaptersPrompt = `You are an expert educational content analyst for an Arabic-language learning platform in Egypt.

Listen carefully to the attached audio file (a full lesson recording) and divide it into logical study chapters.

STRICT RULES:
1. OUTPUT FORMAT: Return ONLY a raw JSON array — no wrapper object, no markdown fences.
   Example: [{"title":"...","startTime":0,"endTime":120,"summaryText":"...","order":1}, ...]
2. QUANTITY: Generate between 5 and 10 chapters. MAXIMUM 15. Group minor sub-topics together.
3. COVERAGE: Chapters must cover 100% of the audio with no gaps and no overlaps.
   - First chapter startTime = 0
   - Last chapter endTime = total audio duration in seconds (rounded to nearest second)
4. TIMESTAMPS: startTime and endTime are integers (seconds). Be precise — use the actual moment the speaker transitions topics.
5. SUMMARIES: Each summaryText must be 3-5 sentences in EGYPTIAN COLLOQUIAL ARABIC (العامية المصرية). Write as if a friendly Egyptian teacher is telling a student what this chapter covers. Use casual, warm language like "هنا هنتعلم..." or "في الجزء ده هنشرح...". Avoid formal/classical Arabic. The tone should feel like a friend explaining, not a textbook.
6. TITLES: Short, descriptive Arabic titles (3-7 words).`;

    const chaptersResponse = await ai.models.generateContent({
        model: 'gemini-2.5-flash',
        contents: [{
            role: 'user',
            parts: [
                { fileData: fileRef },
                { text: chaptersPrompt }
            ]
        }],
        config: {
            responseMimeType: 'application/json',
            responseSchema: {
                type: Type.ARRAY,
                description: 'Chronological chapters. 5–10 items, max 15.',
                items: {
                    type: Type.OBJECT,
                    properties: {
                        title: { type: Type.STRING },
                        startTime: { type: Type.INTEGER, description: 'Start time in seconds' },
                        endTime: { type: Type.INTEGER, description: 'End time in seconds' },
                        summaryText: { type: Type.STRING, description: '3-5 sentence summary in Egyptian colloquial Arabic (عامية مصرية)' },
                        order: { type: Type.INTEGER }
                    },
                    required: ['title', 'startTime', 'endTime', 'summaryText', 'order']
                }
            }
        }
    });

    const chaptersText = (chaptersResponse.text || '').trim();
    if (!chaptersText) {
        throw new Error('[Gemini] CALL B returned empty chapters content.');
    }
    console.log(`[Gemini] CALL B complete. Chapters JSON length: ${chaptersText.length} chars.`);

    // ── 4. Cleanup uploaded file ──────────────────────────────────────────────
    try {
        if (uploadResult.name) {
            await ai.files.delete({ name: uploadResult.name });
            console.log(`[Gemini] Deleted file ${uploadResult.name} from File API.`);
        }
    } catch (e) {
        console.warn(`[Gemini] Warning: Failed to clean up File API file. ${e}`);
    }

    // ── 5. Parse chapters JSON ────────────────────────────────────────────────
    // The schema forces an array, but extract defensively in case of any wrapper.
    let chaptersArray: VideoAIResult['chapters'];
    try {
        const parsed = JSON.parse(chaptersText);
        // Handle both bare array and { chapters: [...] } wrapper
        chaptersArray = Array.isArray(parsed) ? parsed : (parsed.chapters ?? parsed);
        if (!Array.isArray(chaptersArray)) throw new Error('Not an array');
    } catch (e) {
        // Try extracting a JSON array substring as last resort
        const match = chaptersText.match(/\[[\s\S]*\]/);
        if (!match) {
            console.error('[Gemini] CALL B raw output:', chaptersText.slice(0, 500));
            throw new Error('Gemini chapter analysis returned unparseable output.');
        }
        chaptersArray = JSON.parse(match[0]);
    }

    console.log(`[Gemini] Parsed ${chaptersArray.length} chapters successfully.`);
    return { srtContent, chapters: chaptersArray };
}

/**
 * Generates an educational mindmap with the teacher's caricature for a specific chapter.
 */
export async function generateChapterMindmap(
    chapter: { title: string; summaryText: string; order: number },
    lessonVideoId: string,
    teacherPhotoPath?: string
): Promise<string | null> {
    try {
        console.log(`[Gemini] Generating mindmap for chapter: ${chapter.title}`);

        let parts: any[] = [];

        // ─────────────────────────────────────────────────────────────────────
        // IMPORTANT: Teacher photo MUST come FIRST in the parts array.
        // Gemini image generation models use the first image as the primary
        // visual reference for character likeness. Placing it before the text
        // prompt ensures the model registers it as a "character reference sheet"
        // rather than a generic attachment.
        // ─────────────────────────────────────────────────────────────────────
        if (teacherPhotoPath && fs.existsSync(teacherPhotoPath)) {
            const ext = teacherPhotoPath.split('.').pop()?.toLowerCase() || 'jpg';
            const mimeType = ext === 'png' ? 'image/png' : 'image/jpeg';
            const base64Data = fs.readFileSync(teacherPhotoPath).toString('base64');
            // Push photo BEFORE the prompt text
            parts.push({
                inlineData: {
                    mimeType: mimeType,
                    data: base64Data
                }
            });
            console.log(`[Gemini] Teacher photo loaded and placed as FIRST part (reference image).`);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PROMPT: Uses Chain-of-Thought + Instruction Hierarchy + Spatial Rules
        // Pattern: [System Role] → [Rendering Pipeline] → [Chapter Data]
        //          → [Arabic Text Contract] → [Layout Grid] → [Character Rules]
        //          → [Final Checklist]
        // ─────────────────────────────────────────────────────────────────────
        const hasPhoto = teacherPhotoPath && fs.existsSync(teacherPhotoPath);
        const prompt = `A premium, ultra-high-detail 3D isometric educational mindmap about: "${chapter.title}".
Format Requirement: The generated image MUST be strictly in a 16:9 Widescreen Landscape horizontal format. DO NOT generate portrait or vertical images.
Chapter Context: ${chapter.summaryText}
Style: Pixar, colorful, vibrant, 3D render, glowing volumetric lighting.
Layout: Wide horizontal landscape 16:9 composition. Use the width to spread out the mindmap horizontally.
Background: A beautiful cinematic horizontal environment matching the subject and era of the chapter context. Build a fully realized scene relevant to the topic.
Center: A large elegant glowing central node with the Arabic text "${chapter.title}" written clearly in big, bold, legible text.
Branches: Glowing curved light beams extending from the center, connecting to smaller colorful 3D nodes. Inside each small node, write exactly ONE very short Arabic keyword (max 2 words) extracted from the context.
${hasPhoto ? `Characters: A highly detailed, friendly 3D Pixar-style caricature of the teacher (matching the provided reference photo features EXTREMELY closely). The teacher MUST be dressed in an outfit or costume that perfectly matches the subject of the chapter (e.g., historical clothes for history, lab coat for science). They are standing dynamically and pointing toward the center node.` : `Characters: A friendly 3D Pixar-style teacher dressed in an outfit matching the chapter's subject, pointing at the center node.`}
Decorations: Floating thematic elements, subtle sparkles, 8k resolution, masterpiece.

CRITICAL INSTRUCTIONS FOR 100% ACCURATE ARABIC TEXT:
1. Arabic is a Right-to-Left (RTL) language. You MUST write it strictly from right to left.
2. Letters MUST be CONNECTED (cursive). DO NOT write isolated or disconnected letters (e.g., writing "م ص ر" is strictly forbidden; you MUST write "مصر" fully connected).
3. The spelling of "${chapter.title}" must be 100% correct, verified letter-by-letter. No hallucinated characters, no missing dots.
4. For the sub-nodes, write extremely short phrases (1 or 2 words maximum) and ensure perfect Arabic letter connectivity.
5. Typography should be bold, readable, and highly accurate.`;

        parts.push({ text: prompt });

        const response = await ai.models.generateContent({
            model: 'gemini-3-pro-image-preview',
            // @ts-ignore - The types might not fully support this exact struct for standard generation yet
            contents: [
                {
                    role: 'user',
                    parts: parts
                }
            ],
            config: {
                // @ts-ignore
                aspectRatio: '16:9'
            }
        });

        // Loop through response parts to find the image
        if (response.candidates && response.candidates[0]?.content?.parts) {
            for (const part of response.candidates[0].content.parts) {
                if (part.inlineData && part.inlineData.data) {
                    const imageData = part.inlineData.data;
                    const buffer = Buffer.from(imageData, 'base64');

                    // Save locally
                    const mindmapsDir = path.resolve(process.cwd(), '../backend/src/NaderGorge.API/wwwroot/mindmaps');
                    if (!fs.existsSync(mindmapsDir)) {
                        fs.mkdirSync(mindmapsDir, { recursive: true });
                    }

                    const fileName = `${lessonVideoId}_chapter_${chapter.order}_${Date.now()}.png`;
                    const filePath = path.join(mindmapsDir, fileName);

                    fs.writeFileSync(filePath, buffer);
                    console.log(`[Gemini] Mindmap solved and saved locally: ${filePath}`);

                    // Return the public relative URL
                    return `/mindmaps/${fileName}`;
                }
            }
        }

        console.warn(`[Gemini] No valid image returned for chapter ${chapter.order}`);
        return null;

    } catch (e) {
        console.error(`[Gemini] Failed to generate mind map for chapter ${chapter.order}:`, e);
        return null;
    }
}
