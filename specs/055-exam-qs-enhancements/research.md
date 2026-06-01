# Research & Technical Decisions: Exam and Question UI Enhancements

## 1. Storage for Voice Collections (Audio Uploads)
**Decision**: Use the existing file storage mechanism (e.g., S3-compatible Blob storage or direct uploads, managed through a `.NET` API endpoint that returns a URL). The `audioUrl` will be saved on the `Question` entity.
**Rationale**: Reusing existing storage patterns maintains architectural consistency and keeps the database size manageable by only storing file URLs.
**Alternatives considered**: Base64 encoding in the database (rejected due to size limitations and database bloat), direct streaming (overkill for simple voice notes).

## 2. "Find the Mistake" UI Component
**Decision**: Build a custom React component for the frontend that renders text as an array of selectable tokens or spans. The correct answer is defined by the backend using character start/end indices or a specific word token.
**Rationale**: Simple text highlighting via tokenization provides a robust, accessible, and touch-friendly experience on both mobile and desktop.
**Alternatives considered**: Using an image hotspot (rejected as not text-based), using a raw `<textarea>` with selection range (error-prone on mobile).

## 3. AI Essay Grading Pipeline
**Decision**: Offload the AI grading to the existing Node.js BullMQ worker utilizing the `@google/genai` SDK. The backend C# API will enqueue a job on essay submission, the worker will grade via Gemini, and ping a webhook or update the DB to record the initial grade and feedback.
**Rationale**: Aligns strictly with the architectural guidelines from the Constitution (Background Jobs section). Prevents the primary C# API from blocking on LLM inference.
**Alternatives considered**: Calling Gemini directly from C# synchronously (rejected due to potential timeout and blocking of API threads).

## 4. Hint Reveal Interaction
**Decision**: Store hints in the database on the `Question` entity. Only return the hint content on demand (e.g., via a separate fetch) or hide it securely in the frontend state if pre-loaded, exposing it when the user clicks 'Show Hint'.
**Rationale**: Pre-loading hints is generally fine for standard homework, but for strict exams, it can be fetched on demand to prevent tampering via React DevTools.
**Alternatives considered**: Only fetching hints when clicked (better security, slightly higher latency). 
