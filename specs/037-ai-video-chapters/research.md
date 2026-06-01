# Phase 0: Research & Technical Architecture

## 1. Background Jobs & Processing Environment (Node.js Worker vs .NET)

**Decision**: The actual audio extraction (FFmpeg) and Gemini AI communication will be handled entirely by the **Node.js Worker (BullMQ)**, not the .NET Backend API.

**Rationale**:
The project Constitution clearly mandates: `.NET writes jobs → Redis broker → Node worker processes`. 
Video processing (downloading, FFmpeg extraction) is CPU/IO-bound and long-running. Running this inside the .NET Web API would risk thread pool exhaustion. By pushing it to the Node.js worker:
- The .NET API simply enqueues an `ai-video-chapters-job` with the `LessonVideoId` and the video source URL.
- The Node worker picks it up, uses `fluent-ffmpeg` to download/extract MP3.
- The Node worker uses the official Google Gen AI SDK (`@google/genai`) to upload to the File API and prompt Flash 2.5.
- Upon completion, the Node worker saves the output (Chapters, Summaries, and SRT URL) to the PostgreSQL database (perhaps via an internal REST endpoint on the .NET API or directly via Prisma/TypeORM if configured).

**Alternatives considered**: 
Doing it in C# using Hangfire. Rejected because the Constitution strictly adopts BullMQ (Node worker) for background jobs.

---

## 2. Audio Extraction via FFmpeg

**Decision**: Use `fluent-ffmpeg` in the Node.js worker to extract audio as a highly compressed mono MP3 (e.g., 32kbps mono).

**Rationale**: 
Audio clarity is minimally impacted by low bitrates when the goal is speech-to-text. A 1-hour video at 32k mono is roughly 14MB. This makes the upload to the Gemini File API nearly instantaneous.

**Alternatives considered**:
Using cloud media-convert services, or extracting audio on the client side. Client-side is impossible securely, and cloud services add unnecessary cost when we have a Node worker capable of running FFmpeg.

---

## 3. Gemini API Integration

**Decision**: Use the `@google/genai` Node SDK's `fileManager` and `models.generateContent`.

**Rationale**:
Gemini Flash 1.5/2.5 supports up to 2M tokens and native audio files.
1. `fileManager.uploadFile(audioPath)` returns a URI.
2. Prompt Gemini: "Return a JSON object with 'srtContent' (the raw SRT string), 'chapters' (array of start, end, title, summary translated to Arabic)."
3. Gemini returns the structured JSON. The Node worker parses it, saves the SRT string as an `.srt` file to cloud/local storage, and sends the URL + Chapters to the Database.

**Alternatives considered**:
Using OpenAI Whisper. The user explicitly requested using Gemini exclusively to save costs and consolidate into a single, highly capable pipeline.

---

## 4. Video Player Integration (Next.js)

**Decision**: Pass the generated `VideoChapters` array to the frontend iframe (`embed/route.ts` or `VideoPlayer.tsx`), and overlay interactive CSS markers on the progress bar. Add a `ChapterList` supplementary panel synchronized with the player's `currentTime`.

**Rationale**:
Modern HTML5 players (and standard UI packages) support seeking via `video.currentTime = X`. The chapter boundaries provide a structured way for the UI to update the "active chapter".
