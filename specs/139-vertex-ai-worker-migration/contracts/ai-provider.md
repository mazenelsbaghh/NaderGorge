# Internal AI Provider Contract

The worker exposes no new public API. This contract defines the internal service boundary used by existing job processors.

```ts
type AIOperation = 'transcription' | 'chapters' | 'essay' | 'mindmap';

interface AIProviderGateway {
  execute<T>(input: {
    operation: AIOperation;
    vertex: () => Promise<T>;
    developer: () => Promise<T>;
  }): Promise<T>;
}
```

Rules:

1. `vertex` executes first when `AI_PRIMARY_PROVIDER=vertex`.
2. `developer` executes at most once only if the Vertex exception is structurally classified as quota exhausted.
3. Developer-primary mode directly executes `developer` and never invokes Vertex as an automatic fallback.
4. Missing fallback credentials after a quota error produces a sanitized combined diagnostic.
5. Permission, authentication, validation, safety, unsupported model/region, malformed response, parsing, persistence, callback, and cancellation failures never trigger fallback.
6. The gateway never logs request prompts, student answers, API keys, credentials, GCS URI, signed URL, or raw provider response bodies.
7. Existing exported functions remain compatible:
   - `analyzeVideoChapters(audioFilePath, correlationId?) → VideoAIResult`
   - `evaluateEssay(answerText, expectedAnswer?) → {isCorrect, feedback}`
   - `generateChapterMindmap(chapter, lessonVideoId, teacherPhotoPath?) → string | null`

The optional correlation ID is internal and does not alter BullMQ payloads or backend callbacks.
