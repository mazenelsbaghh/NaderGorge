# Quickstart

This guide explains how to queue an AI Video Chapter generation job and retrieve the results in the Next.js Frontend.

## 1. Prerequisites (Admin Configuration)

To make this work, the backend must be connected to Redis (for BullMQ) and have the Node Worker running.

1. Ensure the Node.js Background Worker is active `npm start` in the worker directory.
2. The Node worker environment variables must have:
   * `GEMINI_API_KEY=AIzaSyB...`
   * `REDIS_URL=redis://localhost:6379`
   * `API_CALLBACK_SECRET=secretxyz`

## 2. Triggering the Generation (C# Backend)

Admins can trigger the task from the swagger UI or the React dashboard:
```http
POST /api/V1/admin/lessons/videos/123e4567-e89b-12d3.../generate-ai
```
This sets `IsProcessingAI = true` on the video and pushes the `LessonVideoId` + Source URL to Redis.

## 3. Worker Processing (Node.js)

The BullMQ process will:
1. Hit the Source URL (Telegram or YouTube via tools like `ytdl-core` or pure HTTP).
2. Pipe the stream into `ffmpeg` to extract a 32kbps MP3 file.
3. Call `genai.fileManager.uploadFile()`.
4. Call `genai.generateContent({ model: "gemini-2.5-flash", ... })` to get the JSON parts.
5. Hit the Callback Webhook on the .NET API to insert the chapters.

## 4. Frontend Rendering (Next.js Player)

When the students load the lesson, the `VideoChapters` array will be populated in the API response.

* **Progress Bar**: Calculate the `%` width for each chapter based on `(EndTime - StartTime) / TotalDuration * 100` and overlay standard div components.
* **Navigation**: Clicking an item in the supplementary sidebar executes `videoElement.currentTime = chapter.StartTime;`.
