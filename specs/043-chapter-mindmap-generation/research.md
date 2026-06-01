# Research & Decisions: Chapter Mindmap Generation

## 1. Gemini Image Generation Integration

**Decision**: We will use the Google Gen AI SDK (`@google/genai`) to integrate with the new `gemini-3.1-flash-image-preview` model via `ai.models.generateContent`.
**Rationale**: The user explicitly provided the snippet for this model. This outputs image responses as base64 encoded `inlineData` within the response `parts` which is directly converted and written to the filesystem.

## 2. Teacher Caricature Reference & Storage Strategy

**Decision**: All generated mind maps and reference photos will be stored strictly on local storage (`node:fs` / `Buffer.from(imageData, "base64")`). We will build an image upload mechanism in the Admin Dashboard to store teacher reference images purely in local server storage (`.tmp/` or similar storage paths). 
**Rationale**: The user specifically requested local storage only ("خلي التخزين محلي"). S3 is out of scope.

## 3. Arabic Mind Map Prompt Engineering

**Decision**: We will utilize the **Few-Shot Learning** and **Template Systems** techniques from the `prompt-engineering` workflow. The prompt template will explicitly instruct the model to:
1. Generate an educational mind map.
2. Render all text strictly in Arabic.
3. Include a caricature of the teacher.
4. Visually connect the subtopics of the chapter.
**Rationale**: High-quality prompts dictate output quality. Structuring the prompt with explicit negative and positive constraints ensures consistency in educational settings.

## 4. UI Display Strategy

**Decision**: We will implement a dual-display approach in the frontend:
- **Overlay**: Use `framer-motion` in `PlayerControls.tsx` or `SecureVideoPlayer.tsx` to display a transient modal/overlay when the progress hits the end of a chapter.
- **Persistent Section**: Add a new responsive section beneath `LessonCarousel.tsx` (or the video container) that listens to the `activeChapter` state and displays the corresponding mind map image.
**Rationale**: Covers both active engagement (in-video popups) and passive review (persistent scrolling map below the video) as requested by the user.
