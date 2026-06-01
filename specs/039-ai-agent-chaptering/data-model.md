# Entities and Data Model

## AIAgentWorkflowState (Redis / BullMQ Data Payload)

The existing `VideoAnalysisJob` payload inside BullMQ's `ai-video-chapters` queue will be augmented with persistent state attributes so the job can recover from partial completion when retried.

```typescript
interface AIAgentWorkflowState {
  // Original Inputs
  lessonVideoId: string;
  sourceUrl: string;

  // Persistent Stage Caches (Populated progressively)
  audioPath?: string;        // Absolute path to the extracted .mp3. If exists, skip extraction.
  aiRawResponse?: any;       // Raw JSON output from Gemini. If exists, skip Gemini generation.
  srtContent?: string;       // Generated SRT. If exists, skip generation.
  subtitleUrl?: string;      // The path /subtitles/... where the SRT is accessible to the frontend.
}
```

## Frontend Progress State

The new structure returned by the `GET /api/status/:id` worker status endpoint.

```typescript
type AIProgressPayload = {
  id: string; // lessonVideoId
  state: 'waiting' | 'active' | 'completed' | 'failed' | 'delayed' | 'not_found';
  
  // Progress is now an object, not a flat number. The worker endpoint will serialize it.
  progress: {
    percentage: number;      // 0-100
    stage: string;           // e.g. "جاري تحميل وفصل الصوت...", "الذكاء الاصطناعي يقوم بتحليل الفيديو..."
  } | number; // Fallback for backward compatibility just in case
  
  failedReason?: string;
};
```
