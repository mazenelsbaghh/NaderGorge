import { GoogleGenAI, Type } from '@google/genai';
import { Agent, setGlobalDispatcher } from 'undici';
import fs from 'fs';
import path from 'path';
import { randomUUID } from 'node:crypto';
import { execFileSync, execSync } from 'child_process';
import { readAIConfig, type AIConfig } from './aiConfig.js';
import { executeGeminiRequest, GeminiDeveloperApiError } from './aiProvider.js';
import { classifyAIError } from './aiErrors.js';
import type { LiveSupportAgentPrompt } from './liveSupportAgent.js';
import { parseLiveSupportDecision, type LiveSupportDecision } from './liveSupportDecisionSchema.js';
import { atomicWriteFileSync, sharedMindmapsRoot } from '../config/storage.js';

const providerTimeoutMs = Number.parseInt(process.env.AI_PROVIDER_TIMEOUT_MS || '600000', 10);

setGlobalDispatcher(new Agent({
  connectTimeout: providerTimeoutMs,
  headersTimeout: providerTimeoutMs,
  bodyTimeout: providerTimeoutMs,
}));

type GenAIClient = Pick<GoogleGenAI, 'models'>;
type GeneratedContent = Awaited<ReturnType<GenAIClient['models']['generateContent']>>;
type InlineAudioData = { mimeType: string; data: string };
// Gemini direct inline requests are capped at 20 MB. Reserve room for JSON/prompt overhead.
const MAX_INLINE_AUDIO_BYTES = 14 * 1024 * 1024;

interface AudioGenerationRequest {
  operation: 'transcription' | 'chapters';
  prompt: string;
  responseMimeType: 'text/plain' | 'application/json';
  responseSchema?: typeof chapterSchema;
}

interface AIRuntime {
  config: AIConfig;
  developer: GenAIClient;
}

type RuntimeFactory = () => AIRuntime;
let runtimeFactory: RuntimeFactory | undefined;

function createClient(options: ConstructorParameters<typeof GoogleGenAI>[0]) {
  return new GoogleGenAI({ ...options, httpOptions: { timeout: providerTimeoutMs } });
}

function createRuntime(): AIRuntime {
  if (runtimeFactory) return runtimeFactory();
  const config = readAIConfig();
  const developer = createClient({ apiKey: config.developerApiKey });
  return {
    config,
    developer,
  };
}

export function setAIServiceRuntimeFactoryForTests(factory?: RuntimeFactory) {
  runtimeFactory = factory;
}

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

export interface EssayAIResult {
  isCorrect: boolean;
  feedback: string;
}

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

const chaptersPrompt = `You are an expert educational content analyst for an Arabic-language learning platform in Egypt.

Listen carefully to the attached audio file (a full lesson recording) and divide it into logical study chapters.

STRICT RULES:
1. OUTPUT FORMAT: Return ONLY a raw JSON array — no wrapper object, no markdown fences.
2. QUANTITY: Generate between 5 and 10 chapters. MAXIMUM 15. Group minor sub-topics together.
3. COVERAGE: Chapters must cover 100% of the audio with no gaps and no overlaps.
   - First chapter startTime = 0
   - Last chapter endTime = total audio duration in seconds (rounded to nearest second)
4. TIMESTAMPS: startTime and endTime are integers (seconds). Be precise — use the actual moment the speaker transitions topics.
5. SUMMARIES: Each summaryText must be 3-5 sentences in EGYPTIAN COLLOQUIAL ARABIC (العامية المصرية). Write as if a friendly Egyptian teacher is telling a student what this chapter covers. Use casual, warm language like "هنا هنتعلم..." or "في الجزء ده هنشرح...". Avoid formal/classical Arabic.
6. TITLES: Short, descriptive Arabic titles (3-7 words).`;

const chapterSchema = {
  type: Type.ARRAY,
  items: {
    type: Type.OBJECT,
    properties: {
      title: { type: Type.STRING },
      startTime: { type: Type.INTEGER },
      endTime: { type: Type.INTEGER },
      summaryText: { type: Type.STRING },
      order: { type: Type.INTEGER },
    },
    required: ['title', 'startTime', 'endTime', 'summaryText', 'order'],
  },
};

function parseChapters(text: string): VideoAIResult['chapters'] {
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch {
    const match = text.match(/\[[\s\S]*\]/);
    if (!match) throw new Error('AI chapter analysis returned unparseable output.');
    parsed = JSON.parse(match[0]);
  }
  const chapters = Array.isArray(parsed) ? parsed : (parsed as { chapters?: unknown })?.chapters;
  if (!Array.isArray(chapters)) throw new Error('AI chapter analysis did not return an array.');
  return chapters as VideoAIResult['chapters'];
}

class InlineAudioFile {
  private compressedPath?: string;
  private inlineData?: InlineAudioData;

  constructor(private readonly audioFilePath: string) {}

  reference(): InlineAudioData {
    if (!this.inlineData) {
      const target = path.join(path.dirname(this.audioFilePath), `.${path.basename(this.audioFilePath)}.${randomUUID()}.inline.mp3`);
      try {
        execFileSync('ffmpeg', ['-y', '-i', this.audioFilePath, '-vn', '-ac', '1', '-ar', '16000', '-b:a', '16k', target], { stdio: 'ignore', timeout: providerTimeoutMs });
      } catch {
        throw new Error('Could not compress audio for the direct Gemini request.');
      }
      const size = fs.statSync(target).size;
      if (size > MAX_INLINE_AUDIO_BYTES) {
        fs.unlinkSync(target);
        throw new Error('Lesson audio remains too large for Gemini inline input; split the lesson before analysis.');
      }
      this.compressedPath = target;
      this.inlineData = { mimeType: 'audio/mpeg', data: fs.readFileSync(target).toString('base64') };
    }
    return this.inlineData;
  }

  delete() {
    if (this.compressedPath && fs.existsSync(this.compressedPath)) fs.unlinkSync(this.compressedPath);
  }
}

async function generateAudioContent(
  runtime: AIRuntime,
  inlineAudio: InlineAudioFile,
  generation: AudioGenerationRequest,
): Promise<GeneratedContent> {
  const requestFor = (audio: InlineAudioData) => ({
    model: runtime.config.textModel,
    contents: [{ role: 'user', parts: [{ inlineData: audio }, { text: generation.prompt }] }],
    config: { responseMimeType: generation.responseMimeType, ...(generation.responseSchema ? { responseSchema: generation.responseSchema } : {}) },
  });
  return executeGeminiRequest(() => runtime.developer.models.generateContent(requestFor(inlineAudio.reference())));
}

export async function analyzeVideoChapters(audioFilePath: string): Promise<VideoAIResult> {
  const runtime = createRuntime();
  const developerAudio = new InlineAudioFile(audioFilePath);
  try {
    const srtResponse = await generateAudioContent(runtime, developerAudio, {
      operation: 'transcription',
      prompt: srtPrompt,
      responseMimeType: 'text/plain',
    });
    const srtContent = (srtResponse.text || '').trim();
    if (!srtContent) throw new Error('AI transcription returned empty SRT content.');
    const chaptersResponse = await generateAudioContent(runtime, developerAudio, {
      operation: 'chapters',
      prompt: chaptersPrompt,
      responseMimeType: 'application/json',
      responseSchema: chapterSchema,
    });
    const chaptersText = (chaptersResponse.text || '').trim();
    if (!chaptersText) throw new Error('AI chapter analysis returned empty content.');
    return { srtContent, chapters: parseChapters(chaptersText) };
  } finally {
    developerAudio.delete();
  }
}

function essayEvaluationPrompt(answerText: string, expectedAnswer?: string, questionText?: string) {
  return `You are a friendly Egyptian Arabic teacher who speaks in Egyptian colloquial Arabic (العامية المصرية).
The student has submitted an answer to an essay question.

Question:
${questionText || 'نص السؤال غير متوفر.'}

Teacher's Expected Answer / Key concepts:
${expectedAnswer || 'مفيش إجابة نموذجية متوفرة، قيّم الإجابة على أساس المنطق العام.'}

Student Answer:
${answerText}

Task:
1. Determine if the student's answer is correct based on the question and expected answer.
2. Provide a short 1-2 sentence feedback in EGYPTIAN COLLOQUIAL ARABIC (العامية المصرية). Use a warm, encouraging tone like a friend talking.
IMPORTANT: You MUST NOT write the correct answer in your feedback. Simply tell them if their logic is correct or incorrect and briefly why in general terms.

Return the result STRICTLY as a JSON object with this shape:
{"isCorrect": boolean, "feedback": "string"}
Do not return any markdown code blocks, just raw JSON.`;
}

export async function evaluateEssayWithAI(answerText: string, expectedAnswer?: string, questionText?: string): Promise<EssayAIResult> {
  const runtime = createRuntime();
  const request = { model: runtime.config.textModel, contents: essayEvaluationPrompt(answerText, expectedAnswer, questionText), config: { responseMimeType: 'application/json' } };
  const response = await executeGeminiRequest(() => runtime.developer.models.generateContent(request));
  const parsed = JSON.parse(response.text || '{}') as Partial<EssayAIResult>;
  if (typeof parsed.isCorrect !== 'boolean' || typeof parsed.feedback !== 'string' || !parsed.feedback.trim()) {
    throw new Error('AI essay evaluation returned an invalid result.');
  }
  return { isCorrect: parsed.isCorrect, feedback: parsed.feedback };
}

function mindmapParts(chapter: { title: string; summaryText: string }, teacherPhotoPaths?: string[]) {
  const parts: Array<Record<string, unknown>> = [];
  let hasPhoto = false;
  if (teacherPhotoPaths && teacherPhotoPaths.length > 0) {
    for (const photoPath of teacherPhotoPaths) {
      if (photoPath && fs.existsSync(photoPath)) {
        const mimeType = photoPath.toLowerCase().endsWith('.png') ? 'image/png' : 'image/jpeg';
        parts.push({ inlineData: { mimeType, data: fs.readFileSync(photoPath).toString('base64') } });
        hasPhoto = true;
      }
    }
  }
  parts.push({ text: mindmapPrompt(chapter, hasPhoto) });
  return parts;
}

function mindmapPrompt(chapter: { title: string; summaryText: string }, hasPhoto: boolean) {
  return `A premium, ultra-high-detail 3D isometric educational mindmap about: "${chapter.title}".
Format Requirement: The generated image MUST be strictly in a 16:9 Widescreen Landscape horizontal format. DO NOT generate portrait or vertical images.
Chapter Context: ${chapter.summaryText}
Style: Pixar, colorful, vibrant, 3D render, glowing volumetric lighting.
Layout: Wide horizontal landscape 16:9 composition. Use the width to spread out the mindmap horizontally.
Background: A beautiful cinematic horizontal environment matching the subject and era of the chapter context.
Center: A large elegant glowing central node with the Arabic text "${chapter.title}" written clearly in big, bold, legible text.
Branches: Glowing curved light beams extending from the center, connecting to smaller colorful 3D nodes. Inside each small node, write exactly ONE very short Arabic keyword (max 2 words) extracted from the context.
${hasPhoto ? 'Characters: A highly detailed, friendly 3D Pixar-style caricature of the teacher matching the provided reference images extremely closely (incorporating facial details and style from all of them), dressed for the subject.' : 'Characters: A friendly 3D Pixar-style teacher dressed for the subject.'}
Decorations: Floating thematic elements, subtle sparkles, 8k resolution, masterpiece.

CRITICAL INSTRUCTIONS FOR 100% ACCURATE ARABIC TEXT:
1. Arabic is Right-to-Left. Write it strictly from right to left.
2. Letters MUST be CONNECTED (cursive), never isolated.
3. The spelling of "${chapter.title}" must be exact.
4. Sub-node phrases must be one or two words with correct connectivity.
5. Typography must be bold, readable, and accurate.`;
}

function saveMindmapImage(imageData: string, lessonVideoId: string, chapterOrder: number) {
  const mindmapsDir = sharedMindmapsRoot;
  fs.mkdirSync(mindmapsDir, { recursive: true });

  const oldFiles = fs.readdirSync(mindmapsDir).filter((file) =>
    file.startsWith(`${lessonVideoId}_chapter_${chapterOrder}_`));
  const tempPngName = `${lessonVideoId}_chapter_${chapterOrder}_temp_${Date.now()}.png`;
  const tempPngPath = path.join(mindmapsDir, tempPngName);
  const webpName = `${lessonVideoId}_chapter_${chapterOrder}_${Date.now()}.webp`;
  const webpTemporaryName = `.${webpName}.${process.pid}.tmp.webp`;
  const webpTemporaryPath = path.join(mindmapsDir, webpTemporaryName);

  try {
    atomicWriteFileSync(mindmapsDir, tempPngName, Buffer.from(imageData, 'base64'));
    const ffmpegCmd = `ffmpeg -y -i "${tempPngPath}" -vf "scale='min(1200,iw)':'min(1200,ih)':force_original_aspect_ratio=decrease" -q:v 75 "${webpTemporaryPath}"`;
    execSync(ffmpegCmd, { stdio: 'ignore' });
    const webpBytes = fs.readFileSync(webpTemporaryPath);
    atomicWriteFileSync(mindmapsDir, webpName, webpBytes);
    for (const oldFile of oldFiles) fs.rmSync(path.join(mindmapsDir, oldFile), { force: true });

    console.log(`[AI mindmap] Successfully compressed and saved mindmap as WebP: ${webpName}`);
    return `/mindmaps/${webpName}`;
  } catch (err) {
    console.error('[AI mindmap] Failed to compress mindmap to WebP using ffmpeg, falling back to raw PNG:', err);
    
    // Fallback: if WebP transcode fails, save as original PNG
    const pngName = `${lessonVideoId}_chapter_${chapterOrder}_${Date.now()}.png`;
    atomicWriteFileSync(mindmapsDir, pngName, Buffer.from(imageData, 'base64'));
    for (const oldFile of oldFiles) fs.rmSync(path.join(mindmapsDir, oldFile), { force: true });
    return `/mindmaps/${pngName}`;
  } finally {
    fs.rmSync(tempPngPath, { force: true });
    fs.rmSync(webpTemporaryPath, { force: true });
  }
}

export async function generateChapterMindmap(
  chapter: { title: string; summaryText: string; order: number },
  lessonVideoId: string,
  teacherPhotoPathOrPaths?: string | string[],
): Promise<string> {
  try {
    const runtime = createRuntime();
    const photoPaths = typeof teacherPhotoPathOrPaths === 'string'
      ? [teacherPhotoPathOrPaths]
      : (teacherPhotoPathOrPaths || []);
    const request = {
      model: runtime.config.imageModel,
      contents: [{ role: 'user', parts: mindmapParts(chapter, photoPaths) }],
      config: { aspectRatio: '16:9' },
    } as any;
    const response = await executeGeminiRequest(() => runtime.developer.models.generateContent(request));
    const imagePart = response.candidates?.[0]?.content?.parts?.find((responsePart) => responsePart.inlineData?.data);
    if (!imagePart?.inlineData?.data) {
      throw new Error(`AI image provider returned no image for chapter ${chapter.order}.`);
    }
    return saveMindmapImage(imagePart.inlineData.data, lessonVideoId, chapter.order);
  } catch (error) {
    const failure = classifyAIError(error);
    console.error('[AI mindmap] Chapter generation failed.', {
      order: chapter.order,
      category: error instanceof GeminiDeveloperApiError ? error.category : failure.category,
      status: failure.status,
    });
    throw error;
  }
}

export interface LiveSupportAITurnResult {
  decision: LiveSupportDecision;
  provider: string;
  model: string;
}

const liveSupportDecisionSchema = {
  type: Type.OBJECT,
  properties: {
    schemaVersion: { type: Type.STRING },
    type: { type: Type.STRING, enum: ['reply', 'propose_action', 'request_verification', 'propose_account_creation', 'request_resolution', 'handoff'] },
    messageAr: { type: Type.STRING },
    action: {
      type: Type.OBJECT,
      properties: {
        key: { type: Type.STRING },
        arguments: { type: Type.OBJECT },
        safeEffectSummaryAr: { type: Type.STRING },
        safeConsequenceAr: { type: Type.STRING }
      },
      required: ['key', 'safeEffectSummaryAr']
    },
    verification: {
      type: Type.OBJECT,
      properties: {
        intent: { type: Type.STRING }
      },
      required: ['intent']
    },
    accountCreation: {
      type: Type.OBJECT,
      properties: {
        requestedFields: { type: Type.ARRAY, items: { type: Type.STRING } }
      },
      required: ['requestedFields']
    },
    resolution: {
      type: Type.OBJECT,
      properties: {
        reasonCode: { type: Type.STRING },
        safeSummaryAr: { type: Type.STRING }
      },
      required: ['reasonCode', 'safeSummaryAr']
    },
    handoff: {
      type: Type.OBJECT,
      properties: {
        reasonCode: { type: Type.STRING },
        safeSummaryAr: { type: Type.STRING },
        forced: { type: Type.BOOLEAN }
      },
      required: ['reasonCode', 'safeSummaryAr', 'forced']
    }
  },
  required: ['schemaVersion', 'type']
};

export async function generateLiveSupportReply(prompt: LiveSupportAgentPrompt): Promise<LiveSupportAITurnResult> {
  const runtime = createRuntime();
  const request = {
    model: runtime.config.textModel,
    contents: prompt.contents,
    config: {
      systemInstruction: prompt.systemInstruction,
      responseMimeType: 'application/json',
      responseSchema: liveSupportDecisionSchema,
    }
  };
  const remainingMs = Date.parse(prompt.deadlineAt) - Date.now();
  if (remainingMs <= 0) throw new Error('AI_PROVIDER_DEADLINE_EXCEEDED');
  const execute = () => executeGeminiRequest(() => runtime.developer.models.generateContent(request));
  const withDeadline = async () => {
    const remainingMs = Date.parse(prompt.deadlineAt) - Date.now();
    if (remainingMs <= 0) throw new Error('AI_PROVIDER_DEADLINE_EXCEEDED');
    let timer: NodeJS.Timeout | undefined;
    try {
      return await Promise.race([
        execute(),
        new Promise<never>((_, reject) => {
          timer = setTimeout(() => reject(new Error('AI_PROVIDER_DEADLINE_EXCEEDED')), remainingMs);
        }),
      ]);
    } finally {
      if (timer) clearTimeout(timer);
    }
  };
  const execution = await withDeadline();
  const response = execution;

  const rawText = response.text;
  if (!rawText) {
    throw new Error('AI live support turn returned an empty response.');
  }

  let providerOutput: unknown;
  try {
    providerOutput = JSON.parse(rawText);
  } catch {
    throw new Error('AI_DECISION_NOT_JSON');
  }
  const decision = parseLiveSupportDecision(providerOutput);

  return {
    decision,
    provider: 'developer',
    model: runtime.config.textModel,
  };
}
