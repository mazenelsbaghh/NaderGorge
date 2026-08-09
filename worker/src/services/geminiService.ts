import { FileState, GoogleGenAI, Type } from '@google/genai';
import { Agent, setGlobalDispatcher } from 'undici';
import fs from 'fs';
import path from 'path';
import { randomUUID } from 'node:crypto';
import { execFile, execFileSync, execSync } from 'child_process';
import { promisify } from 'node:util';
import { readAIConfig, type AIConfig } from './aiConfig.js';
import { executeGeminiRequest, executeRetriableGeminiRequest, GeminiDeveloperApiError } from './aiProvider.js';
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

type GenAIClient = Pick<GoogleGenAI, 'models' | 'files'>;
type GeneratedContent = Awaited<ReturnType<GenAIClient['models']['generateContent']>>;
type InlineAudioData = { mimeType: string; data: string };
type AudioPart = { inlineData: InlineAudioData } | { fileData: { fileUri: string; mimeType: string } };
// Gemini direct inline requests are capped at 20 MB. Reserve room for JSON/prompt overhead.
const MAX_INLINE_AUDIO_BYTES = 14 * 1024 * 1024;
const INLINE_AUDIO_BITRATE = '12k';
const execFileAsync = promisify(execFile);

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

export type VideoChapter = VideoAIResult['chapters'][number];

export interface EssayAIResult {
  isCorrect: boolean;
  feedback: string;
}

const srtPrompt = `You are an expert verbatim transcription AI for an educational platform.

Listen to the attached audio file and produce a COMPLETE, verbatim transcription in standard SRT subtitle format.

RULES:
- Output ONLY the raw SRT content. No JSON. No markdown fences. No extra commentary.
- Every spoken word must appear in the subtitles — do not skip, summarize, or omit anything.
- Each subtitle block must follow EXACTLY this format:
  [number]
  [HH:MM:SS,mmm --> HH:MM:SS,mmm]
  [spoken text in its original language]
  [blank line]
- Timestamps must be precise to the millisecond.
- Preserve the language actually spoken in every subtitle cue. English speech MUST remain English and Arabic speech MUST remain Arabic. Never translate speech in either direction.
- Preserve deliberate code-switching and technical terms exactly as spoken. Use the appropriate writing direction for each language.
- Do NOT add any text before block 1 or after the last block.`;

const chaptersPrompt = `You are an expert educational content analyst for an Egyptian learning platform.

Listen carefully to the attached audio file (a full lesson recording) and divide it into logical study chapters.

STRICT RULES:
1. OUTPUT FORMAT: Return ONLY a raw JSON array — no wrapper object, no markdown fences.
2. QUANTITY: Generate between 5 and 10 chapters. MAXIMUM 15. Group minor sub-topics together.
3. COVERAGE: Chapters must cover 100% of the audio with no gaps and no overlaps.
   - First chapter startTime = 0
   - Last chapter endTime = total audio duration in seconds (rounded to nearest second)
4. TIMESTAMPS: startTime and endTime are integers (seconds). Be precise — use the actual moment the speaker transitions topics.
5. LANGUAGE: Detect the dominant language actually spoken by the teacher in this chapter. Write BOTH title and summaryText in that same language. Do not translate an English lesson into Arabic, do not translate an Arabic lesson into English, and do not mix languages unless the teacher deliberately uses a necessary technical term.
6. SUMMARIES: Each summaryText must be 3-5 natural, student-facing sentences. Write in the teacher's voice, as though the teacher is speaking directly to the class and guiding them through the chapter—not as a third-person report about the lesson.
   - For Egyptian Arabic, use warm, clear Egyptian colloquial Arabic such as "هنا هنتعلم..." and "ركزوا معايا..."; avoid formal/classical Arabic.
   - For English, use clear, friendly classroom English such as "In this part, we'll..." and "Notice how...".
   - Preserve the teacher's subject vocabulary, examples, and level of formality without inventing facts.
7. TITLES: Short, descriptive titles in the detected chapter language (3-7 words).`;

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
  private audioPart?: AudioPart;
  private uploadedFileName?: string;

  constructor(private readonly runtime: AIRuntime, private readonly audioFilePath: string) {}

  async reference(): Promise<AudioPart> {
    if (!this.audioPart) {
      const target = path.join(path.dirname(this.audioFilePath), `.${path.basename(this.audioFilePath)}.${randomUUID()}.inline.mp3`);
      try {
        await execFileAsync('ffmpeg', ['-y', '-i', this.audioFilePath, '-vn', '-ac', '1', '-ar', '16000', '-b:a', INLINE_AUDIO_BITRATE, target], { timeout: providerTimeoutMs });
      } catch {
        throw new Error('Could not compress audio for the direct Gemini request.');
      }
      const size = fs.statSync(target).size;
      console.log('[AI audio] Compressed lesson audio for Gemini inline input.', {
        sourceBytes: fs.statSync(this.audioFilePath).size,
        compressedBytes: size,
        bitrate: INLINE_AUDIO_BITRATE,
      });
      this.compressedPath = target;
      if (size > MAX_INLINE_AUDIO_BYTES) {
        this.audioPart = await this.upload(target);
      } else {
        this.audioPart = { inlineData: { mimeType: 'audio/mpeg', data: fs.readFileSync(target).toString('base64') } };
      }
    }
    return this.audioPart;
  }

  private async upload(filePath: string): Promise<AudioPart> {
    let uploaded = await this.runtime.developer.files.upload({ file: filePath, config: { mimeType: 'audio/mpeg' } });
    if (!uploaded.name) throw new Error('Gemini Files API returned an unnamed audio file.');
    const uploadedFileName = uploaded.name;
    this.uploadedFileName = uploadedFileName;
    const deadline = Date.now() + providerTimeoutMs;
    while (uploaded.state === FileState.PROCESSING && Date.now() < deadline) {
      await new Promise((resolve) => setTimeout(resolve, 2_000));
      uploaded = await this.runtime.developer.files.get({ name: uploadedFileName });
    }
    if (uploaded.state !== FileState.ACTIVE || !uploaded.uri) {
      throw new Error(`Gemini could not prepare the uploaded lesson audio: ${uploaded.error?.message || uploaded.state || 'unknown state'}.`);
    }
    return { fileData: { fileUri: uploaded.uri, mimeType: uploaded.mimeType || 'audio/mpeg' } };
  }

  async delete() {
    try {
      if (this.uploadedFileName) await this.runtime.developer.files.delete({ name: this.uploadedFileName });
    } finally {
      if (this.compressedPath && fs.existsSync(this.compressedPath)) fs.unlinkSync(this.compressedPath);
    }
  }
}

async function generateAudioContent(
  runtime: AIRuntime,
  inlineAudio: InlineAudioFile,
  generation: AudioGenerationRequest,
): Promise<GeneratedContent> {
  const audioPart = await inlineAudio.reference();
  const requestFor = (part: AudioPart, abortSignal: AbortSignal) => ({
    model: runtime.config.textModel,
    contents: [{ role: 'user', parts: [part, { text: generation.prompt }] }],
    config: {
      abortSignal,
      responseMimeType: generation.responseMimeType,
      ...(generation.responseSchema ? { responseSchema: generation.responseSchema } : {}),
    },
  });
  console.log('[AI provider] Starting Gemini audio request.', {
    operation: generation.operation,
  });
  return executeRetriableGeminiRequest((abortSignal) => runtime.developer.models.generateContent(requestFor(audioPart, abortSignal)));
}

export async function transcribeVideoAudio(audioFilePath: string): Promise<string> {
  const runtime = createRuntime();
  const developerAudio = new InlineAudioFile(runtime, audioFilePath);
  try {
    const srtResponse = await generateAudioContent(runtime, developerAudio, {
      operation: 'transcription',
      prompt: srtPrompt,
      responseMimeType: 'text/plain',
    });
    const srtContent = (srtResponse.text || '').trim();
    if (!srtContent) throw new Error('AI transcription returned empty SRT content.');
    return srtContent;
  } finally {
    await developerAudio.delete();
  }
}

export async function generateVideoChapters(audioFilePath: string): Promise<VideoAIResult['chapters']> {
  const runtime = createRuntime();
  const developerAudio = new InlineAudioFile(runtime, audioFilePath);
  try {
    const chaptersResponse = await generateAudioContent(runtime, developerAudio, {
      operation: 'chapters',
      prompt: chaptersPrompt,
      responseMimeType: 'application/json',
      responseSchema: chapterSchema,
    });
    const chaptersText = (chaptersResponse.text || '').trim();
    if (!chaptersText) throw new Error('AI chapter analysis returned empty content.');
    return parseChapters(chaptersText);
  } finally {
    await developerAudio.delete();
  }
}


export async function analyzeVideoChapters(audioFilePath: string): Promise<VideoAIResult> {
  const srtContent = await transcribeVideoAudio(audioFilePath);
  const chapters = await generateVideoChapters(audioFilePath);
  return { srtContent, chapters };
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
  const response = await executeGeminiRequest((abortSignal) => runtime.developer.models.generateContent({
    ...request,
    config: { ...request.config, abortSignal },
  }));
  const parsed = JSON.parse(response.text || '{}') as Partial<EssayAIResult>;
  if (typeof parsed.isCorrect !== 'boolean' || typeof parsed.feedback !== 'string' || !parsed.feedback.trim()) {
    throw new Error('AI essay evaluation returned an invalid result.');
  }
  return { isCorrect: parsed.isCorrect, feedback: parsed.feedback };
}

export interface MindmapGenerationOptions {
  visualStyles?: string[];
  teacherStyles?: string[];
}

function mindmapParts(
  chapter: { title: string; summaryText: string; order: number },
  teacherPhotoPaths: string[],
  options: MindmapGenerationOptions,
) {
  const parts: Array<Record<string, unknown>> = [];
  let hasPhoto = false;
  if (teacherPhotoPaths.length > 0) {
    for (const photoPath of teacherPhotoPaths) {
      if (photoPath && fs.existsSync(photoPath)) {
        const mimeType = photoPath.toLowerCase().endsWith('.png') ? 'image/png' : 'image/jpeg';
        parts.push({ inlineData: { mimeType, data: fs.readFileSync(photoPath).toString('base64') } });
        hasPhoto = true;
      }
    }
  }
  parts.push({ text: mindmapPrompt(chapter, hasPhoto, options) });
  return parts;
}

function mindmapPrompt(
  chapter: { title: string; summaryText: string; order: number },
  hasPhoto: boolean,
  options: MindmapGenerationOptions,
) {
  const visualDirections = [
    'a clean editorial infographic with layered paper-cut depth and crisp diagrammatic hierarchy',
    'a cinematic 3D diorama that turns the lesson concept into a meaningful scene',
    'an illustrated scientific notebook spread with labeled visual metaphors and tactile objects',
    'a premium museum-exhibit composition with symbolic artifacts arranged around the concept',
    'a modern motion-design poster with rich spatial depth, purposeful icons, and a clear learning path',
  ];
  const teacherDirections = ['photorealistic', 'cartoon', '3D character', 'digital illustration'];
  const selectedVisualStyles = options.visualStyles?.includes('random')
    ? visualDirections[Math.floor(Math.random() * visualDirections.length)]
    : options.visualStyles?.length
      ? options.visualStyles.join(', ')
    : visualDirections[(Math.max(chapter.order, 1) - 1) % visualDirections.length];
  const selectedTeacherStyles = options.teacherStyles?.includes('random')
    ? teacherDirections[Math.floor(Math.random() * teacherDirections.length)]
    : options.teacherStyles?.length
      ? options.teacherStyles.join(', ')
    : 'photorealistic';

  return `Create one premium educational visual mind map about "${chapter.title}".
Format: strictly 16:9 wide landscape. Never create a portrait or square composition.
Lesson context: ${chapter.summaryText}

LANGUAGE RULE (non-negotiable): Detect the language used in the lesson context. Every visible word in the image—the central title and all labels—MUST use that same language and script. Do not translate it. Do not force Arabic into an English lesson or English into an Arabic lesson. Use only the exact central title "${chapter.title}" and at most 3 short labels, each copied or faithfully condensed from the lesson context.

ART DIRECTION: Combine these selected visual treatments into one coherent composition: ${selectedVisualStyles}. Make the background, objects, symbols, color palette, and visual metaphors specific to the chapter's actual topic, period, subject, examples, and learning goal. Avoid generic classroom scenery, repeated neon branches, stock floating icons, or a one-size-fits-all "AI mind map" look. The illustration must communicate the lesson even before its labels are read.

INFORMATION DESIGN: Put the central idea prominently in the center or strongest focal point. Connect 3-5 distinct concepts with a readable hierarchy and generous spacing. Use relevant objects, diagrams, timelines, processes, maps, formulas, or historical/scientific symbols when the context calls for them. Keep all text large, minimal, high-contrast, and fully inside safe margins; no tiny paragraphs and no illegible pseudo-text.

${hasPhoto
    ? `TEACHER IDENTITY LOCK (highest priority): ALL supplied photos show the same teacher and MUST be considered together as identity references. Show that exact teacher, not an approximation. Preserve facial geometry, face shape, eye shape and spacing, eyebrows, nose, lips, ears, skin tone, hairline, hairstyle, facial hair, glasses, age, body proportions, and every distinguishing mark. Never beautify, age, de-age, slim, widen, replace, merge, or reinterpret the face. Never create a generic person, celebrity, caricature, or different ethnicity. The ONLY allowed changes are clothing, pose, background, lighting, and the explicitly selected rendering treatment. Selected teacher rendering treatments: ${selectedTeacherStyles}. Even for cartoon, 3D, or illustration treatments, identity and recognizable facial features must remain exact. The teacher should support the explanation without blocking the map.`
    : 'TEACHER: Do not add a generic teacher, portrait, or face when no teacher reference image is supplied. Focus entirely on lesson-specific visual concepts.'}

TYPOGRAPHY: Match the detected language. For Arabic, preserve right-to-left direction, connected letters, and correct spelling. For Latin-script languages, use correct left-to-right spelling and punctuation. Never mix scripts unless the source title itself does.

Quality bar: polished, original, topic-specific educational art; coherent lighting and perspective; no watermark, no logo, no duplicated objects.`;
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
  options: MindmapGenerationOptions = {},
): Promise<string> {
  try {
    const runtime = createRuntime();
    const photoPaths = typeof teacherPhotoPathOrPaths === 'string'
      ? [teacherPhotoPathOrPaths]
      : (teacherPhotoPathOrPaths || []);
    const request = {
      model: runtime.config.imageModel,
      contents: [{ role: 'user', parts: mindmapParts(chapter, photoPaths, options) }],
      config: { aspectRatio: '16:9' },
    } as any;
    const response = await executeGeminiRequest((abortSignal) => runtime.developer.models.generateContent({
      ...request,
      config: { ...request.config, abortSignal },
    }));
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
  const execute = () => executeGeminiRequest((abortSignal) => runtime.developer.models.generateContent({
    ...request,
    config: { ...request.config, abortSignal },
  }));
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
