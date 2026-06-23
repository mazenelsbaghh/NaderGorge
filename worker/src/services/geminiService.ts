import { GoogleGenAI, Type } from '@google/genai';
import { Agent, setGlobalDispatcher } from 'undici';
import fs from 'fs';
import path from 'path';
import { execSync } from 'child_process';
import { readAIConfig, type AIConfig } from './aiConfig.js';
import { AIProviderExecutionError, AIProviderGateway } from './aiProvider.js';
import { TemporaryAudioStorage, type TemporaryAnalysisObject } from './temporaryAudioStorage.js';
import { classifyAIError } from './aiErrors.js';

setGlobalDispatcher(new Agent({
  connectTimeout: 60 * 60 * 1000,
  headersTimeout: 60 * 60 * 1000,
  bodyTimeout: 60 * 60 * 1000,
}));

type GenAIClient = Pick<GoogleGenAI, 'models' | 'files'>;
type GeneratedContent = Awaited<ReturnType<GenAIClient['models']['generateContent']>>;
type AudioFileReference = { fileUri: string; mimeType: string };

interface AudioGenerationRequest {
  operation: 'transcription' | 'chapters';
  prompt: string;
  responseMimeType: 'text/plain' | 'application/json';
  responseSchema?: typeof chapterSchema;
}

interface AIRuntime {
  config: AIConfig;
  gateway: AIProviderGateway;
  vertex: GenAIClient;
  developer: GenAIClient;
  temporaryStorage?: TemporaryAudioStorage | undefined;
  developerUploadDelayMs?: number | undefined;
}

type RuntimeFactory = () => AIRuntime;
let runtimeFactory: RuntimeFactory | undefined;

function createClient(options: ConstructorParameters<typeof GoogleGenAI>[0]) {
  return new GoogleGenAI({ ...options, httpOptions: { timeout: 60 * 60 * 1000 } });
}

function createRuntime(): AIRuntime {
  if (runtimeFactory) return runtimeFactory();
  const config = readAIConfig();
  const developer = createClient({ apiKey: config.developerApiKey || config.fallbackApiKey || '' });
  const vertex = config.primaryProvider === 'vertex'
    ? createClient({ vertexai: true, project: config.project!, location: config.location! })
    : developer;
  return {
    config,
    vertex,
    developer,
    gateway: new AIProviderGateway(config),
    temporaryStorage: config.primaryProvider === 'vertex' ? new TemporaryAudioStorage(config) : undefined,
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

class DeveloperAudioFile {
  private upload?: Awaited<ReturnType<GenAIClient['files']['upload']>>;

  constructor(private readonly runtime: AIRuntime, private readonly audioFilePath: string) {}

  async reference(): Promise<AudioFileReference> {
    if (!this.upload) {
      this.upload = await this.runtime.developer.files.upload({
        file: this.audioFilePath,
        config: { mimeType: 'audio/mpeg', displayName: 'LessonAudioTrack' },
      });
      await new Promise((resolve) => setTimeout(resolve, this.runtime.developerUploadDelayMs ?? 5000));
    }
    if (!this.upload.uri) throw new Error('Developer File API upload returned no URI.');
    return { fileUri: this.upload.uri, mimeType: this.upload.mimeType || 'audio/mpeg' };
  }

  async delete() {
    if (!this.upload?.name) return;
    try {
      await this.runtime.developer.files.delete({ name: this.upload.name });
    } catch {
      console.error('[AI provider] Developer File API cleanup failed.');
    }
  }
}

async function generateAudioContent(
  runtime: AIRuntime,
  developerAudio: DeveloperAudioFile,
  vertexFile: AudioFileReference | undefined,
  generation: AudioGenerationRequest,
): Promise<GeneratedContent> {
  const requestFor = (fileData: AudioFileReference) => ({
    model: runtime.config.textModel,
    contents: [{ role: 'user', parts: [{ fileData }, { text: generation.prompt }] }],
    config: { responseMimeType: generation.responseMimeType, ...(generation.responseSchema ? { responseSchema: generation.responseSchema } : {}) },
  });
  return runtime.gateway.execute({
    operation: generation.operation,
    vertex: () => {
      if (!vertexFile) throw new Error('Vertex audio reference is unavailable.');
      return runtime.vertex.models.generateContent(requestFor(vertexFile));
    },
    developer: async () => runtime.developer.models.generateContent(requestFor(await developerAudio.reference())),
  });
}

async function uploadVertexAudio(runtime: AIRuntime, audioFilePath: string, correlationId: string) {
  if (runtime.config.primaryProvider !== 'vertex') return undefined;
  if (!runtime.temporaryStorage) throw new Error('Vertex temporary storage is unavailable.');
  return runtime.temporaryStorage.upload(audioFilePath, correlationId);
}

export async function analyzeVideoChapters(audioFilePath: string, correlationId = 'video-analysis'): Promise<VideoAIResult> {
  const runtime = createRuntime();
  const developerAudio = new DeveloperAudioFile(runtime, audioFilePath);
  let temporaryObject: TemporaryAnalysisObject | undefined;

  try {
    temporaryObject = await uploadVertexAudio(runtime, audioFilePath, correlationId);
    const vertexFileRef = temporaryObject
      ? { fileUri: temporaryObject.uri, mimeType: 'audio/mpeg' }
      : undefined;
    const srtResponse = await generateAudioContent(runtime, developerAudio, vertexFileRef, {
      operation: 'transcription',
      prompt: srtPrompt,
      responseMimeType: 'text/plain',
    });
    const srtContent = (srtResponse.text || '').trim();
    if (!srtContent) throw new Error('AI transcription returned empty SRT content.');
    const chaptersResponse = await generateAudioContent(runtime, developerAudio, vertexFileRef, {
      operation: 'chapters',
      prompt: chaptersPrompt,
      responseMimeType: 'application/json',
      responseSchema: chapterSchema,
    });
    const chaptersText = (chaptersResponse.text || '').trim();
    if (!chaptersText) throw new Error('AI chapter analysis returned empty content.');
    return { srtContent, chapters: parseChapters(chaptersText) };
  } finally {
    await developerAudio.delete();
    if (temporaryObject && runtime.temporaryStorage) await runtime.temporaryStorage.delete(temporaryObject);
  }
}

function essayEvaluationPrompt(answerText: string, expectedAnswer?: string) {
  return `You are a friendly Egyptian Arabic teacher who speaks in Egyptian colloquial Arabic (العامية المصرية).
The student has submitted an answer to an essay question.

Teacher's Expected Answer / Key concepts:
${expectedAnswer || 'مفيش إجابة نموذجية متوفرة، قيّم الإجابة على أساس المنطق العام.'}

Student Answer:
${answerText}

Task:
1. Determine if the student's answer is correct based on the expected answer.
2. Provide a short 1-2 sentence feedback in EGYPTIAN COLLOQUIAL ARABIC (العامية المصرية). Use a warm, encouraging tone like a friend talking.
IMPORTANT: You MUST NOT write the correct answer in your feedback. Simply tell them if their logic is correct or incorrect and briefly why in general terms.

Return the result STRICTLY as a JSON object with this shape:
{"isCorrect": boolean, "feedback": "string"}
Do not return any markdown code blocks, just raw JSON.`;
}

export async function evaluateEssayWithAI(answerText: string, expectedAnswer?: string): Promise<EssayAIResult> {
  const runtime = createRuntime();
  const request = { model: runtime.config.textModel, contents: essayEvaluationPrompt(answerText, expectedAnswer), config: { responseMimeType: 'application/json' } };
  const response = await runtime.gateway.execute({
    operation: 'essay',
    vertex: () => runtime.vertex.models.generateContent(request),
    developer: () => runtime.developer.models.generateContent(request),
  });
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
  const mindmapsDir = path.resolve(process.cwd(), '../backend/src/NaderGorge.API/wwwroot/mindmaps');
  fs.mkdirSync(mindmapsDir, { recursive: true });

  // 1. Delete old mindmap files for this specific chapter
  try {
    const files = fs.readdirSync(mindmapsDir);
    const prefix = `${lessonVideoId}_chapter_${chapterOrder}_`;
    for (const file of files) {
      if (file.startsWith(prefix)) {
        fs.unlinkSync(path.join(mindmapsDir, file));
        console.log(`[AI mindmap] Deleted old mindmap file: ${file}`);
      }
    }
  } catch (err) {
    console.error('[AI mindmap] Failed to clean up old mindmap files:', err);
  }

  // 2. Transcode to compressed WebP using ffmpeg
  const tempPngName = `${lessonVideoId}_chapter_${chapterOrder}_temp_${Date.now()}.png`;
  const tempPngPath = path.join(mindmapsDir, tempPngName);
  const webpName = `${lessonVideoId}_chapter_${chapterOrder}_${Date.now()}.webp`;
  const webpPath = path.join(mindmapsDir, webpName);

  try {
    // Write temporary PNG file
    fs.writeFileSync(tempPngPath, Buffer.from(imageData, 'base64'));

    // Execute ffmpeg to compress and resize to WebP
    // We scale down using scale filter (max 1200 px in either dimension)
    const ffmpegCmd = `ffmpeg -y -i "${tempPngPath}" -vf "scale='min(1200,iw)':'min(1200,ih)':force_original_aspect_ratio=decrease" -q:v 75 "${webpPath}"`;
    execSync(ffmpegCmd, { stdio: 'ignore' });

    console.log(`[AI mindmap] Successfully compressed and saved mindmap as WebP: ${webpName}`);
    return `/mindmaps/${webpName}`;
  } catch (err) {
    console.error('[AI mindmap] Failed to compress mindmap to WebP using ffmpeg, falling back to raw PNG:', err);
    
    // Fallback: if WebP transcode fails, save as original PNG
    const pngName = `${lessonVideoId}_chapter_${chapterOrder}_${Date.now()}.png`;
    const pngPath = path.join(mindmapsDir, pngName);
    fs.writeFileSync(pngPath, Buffer.from(imageData, 'base64'));
    return `/mindmaps/${pngName}`;
  } finally {
    // Clean up temporary PNG
    if (fs.existsSync(tempPngPath)) {
      try {
        fs.unlinkSync(tempPngPath);
      } catch (e) {
        console.error('[AI mindmap] Failed to delete temp PNG file:', e);
      }
    }
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
    const response = await runtime.gateway.execute({
      operation: 'mindmap',
      vertex: () => runtime.vertex.models.generateContent(request),
      developer: () => runtime.developer.models.generateContent(request),
    });
    const imagePart = response.candidates?.[0]?.content?.parts?.find((responsePart) => responsePart.inlineData?.data);
    if (!imagePart?.inlineData?.data) {
      throw new Error(`AI image provider returned no image for chapter ${chapter.order}.`);
    }
    return saveMindmapImage(imagePart.inlineData.data, lessonVideoId, chapter.order);
  } catch (error) {
    const failure = classifyAIError(error);
    console.error('[AI mindmap] Chapter generation failed.', {
      order: chapter.order,
      category: error instanceof AIProviderExecutionError ? error.primaryCategory : failure.category,
      status: failure.status,
    });
    throw error;
  }
}

export interface LiveSupportAIDecisionHandoff {
  reasonCode: string;
  safeSummaryAr: string;
}

export interface LiveSupportAIDecisionAction {
  key: string;
  arguments?: Record<string, any> | undefined;
  safeEffectSummaryAr: string;
}

export interface LiveSupportAIDecisionVerification {
  intent: string;
}

export interface LiveSupportAIDecisionAccountCreation {
  requestedFields: string[];
}

export interface LiveSupportAIDecision {
  type: 'reply' | 'propose_action' | 'request_verification' | 'propose_account_creation' | 'handoff';
  messageAr?: string | undefined;
  action?: LiveSupportAIDecisionAction | undefined;
  verification?: LiveSupportAIDecisionVerification | undefined;
  accountCreation?: LiveSupportAIDecisionAccountCreation | undefined;
  handoff?: LiveSupportAIDecisionHandoff | undefined;
}

export interface LiveSupportAITurnResult {
  decision: LiveSupportAIDecision;
  provider: string;
  model: string;
}

const liveSupportDecisionSchema = {
  type: Type.OBJECT,
  properties: {
    schemaVersion: { type: Type.STRING },
    type: { type: Type.STRING, enum: ['reply', 'propose_action', 'request_verification', 'propose_account_creation', 'handoff'] },
    messageAr: { type: Type.STRING },
    action: {
      type: Type.OBJECT,
      properties: {
        key: { type: Type.STRING },
        arguments: { type: Type.OBJECT },
        safeEffectSummaryAr: { type: Type.STRING }
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
    handoff: {
      type: Type.OBJECT,
      properties: {
        reasonCode: { type: Type.STRING },
        safeSummaryAr: { type: Type.STRING }
      },
      required: ['reasonCode', 'safeSummaryAr']
    }
  },
  required: ['schemaVersion', 'type']
};

export async function generateLiveSupportReply(
  systemInstructions: string,
  knowledgeDocuments: string[],
  messages: Array<{ senderType: string; content: string; sentAt: string }>,
): Promise<LiveSupportAITurnResult> {
  const runtime = createRuntime();

  const systemInstructionText = `${systemInstructions}

Available Knowledge/Context Documents:
${knowledgeDocuments.map((doc, idx) => `--- DOCUMENT ${idx + 1} ---\n${doc}`).join('\n\n')}

CRITICAL DIRECTIVES:
1. Always reply in warm, helpful, Egyptian colloquial Arabic (العامية المصرية).
2. For normal responses to the user, you MUST set the JSON 'type' property to "reply" and put your Arabic response message in 'messageAr'.
3. If the user explicitly asks to talk to a human, or if you cannot answer their question after searching the provided documents, or if they present a complex issue, set the JSON 'type' property to "handoff" and populate the 'handoff' object with 'reasonCode' and 'safeSummaryAr'.
4. Your response MUST strictly follow the JSON response schema.`;

  const contents = messages.map(m => {
    const role = (m.senderType === 'Student' || m.senderType === 'Guest') ? 'user' : 'model';
    return {
      role,
      parts: [{ text: m.content }]
    };
  });

  const request = {
    model: runtime.config.textModel,
    contents,
    config: {
      systemInstruction: systemInstructionText,
      responseMimeType: 'application/json',
      responseSchema: liveSupportDecisionSchema,
    }
  };

  const response = await runtime.gateway.execute({
    operation: 'live-support',
    vertex: () => runtime.vertex.models.generateContent(request),
    developer: () => runtime.developer.models.generateContent(request),
  });

  const rawText = response.text;
  if (!rawText) {
    throw new Error('AI live support turn returned an empty response.');
  }

  const parsed = JSON.parse(rawText) as {
    type: string;
    messageAr?: string;
    action?: any;
    verification?: any;
    accountCreation?: any;
    handoff?: { reasonCode: string; safeSummaryAr: string };
  };

  let decisionType = parsed.type;
  if (decisionType === 'message' || decisionType === 'messageAr') {
    decisionType = 'reply';
  }

  const allowedTypes = ['reply', 'propose_action', 'request_verification', 'propose_account_creation', 'handoff'];
  if (!allowedTypes.includes(decisionType)) {
    throw new Error(`AI live support turn returned invalid decision type: ${parsed.type}`);
  }

  const decision: LiveSupportAIDecision = {
    type: decisionType as any
  };
  if (parsed.messageAr !== undefined) {
    decision.messageAr = parsed.messageAr;
  }
  if (parsed.action !== undefined) {
    decision.action = parsed.action;
  }
  if (parsed.verification !== undefined) {
    decision.verification = parsed.verification;
  }
  if (parsed.accountCreation !== undefined) {
    decision.accountCreation = parsed.accountCreation;
  }
  if (parsed.handoff !== undefined) {
    decision.handoff = parsed.handoff;
  }

  return {
    decision,
    provider: runtime.config.primaryProvider,
    model: runtime.config.textModel,
  };
}
