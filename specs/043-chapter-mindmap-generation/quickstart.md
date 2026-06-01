# Quickstart: Chapter Mindmap Generation

This guide explains how to get started developing the Chapter Mind Map Generation feature, spanning both the .NET backend API and the Node.js AI worker.

## Backend (C#) Development

1. Update the EF Core context and models to include `TeacherPhoto` and add `MindmapImageUrl` to `LessonChapter`.
2. Create an admin endpoint to allow uploading teacher reference photos (saving them to local storage).
3. Update the `AiAnalysisCompletedCommand` to accept mapped `MindmapImageUrl` values from the worker.
4. Pass the local URL/paths of the active teacher photos into the BullMQ AI job payload.

## AI Worker (Node.js) Development

1. Open `worker/src/services/geminiService.ts`.
2. Inject the `@google/genai` library's `models.generateContent` capability using model `gemini-3.1-flash-image-preview`.
3. Use Prompt Engineering (Few-Shot, Templates) to craft a highly descriptive text prompt:
   - "Generate an educational summary mind map containing the following Arabic text: [Chapter Title]. Add a caricature of a teacher as the central character. The map should be clean, legible, and visually structured."
4. Read the incoming base64 inlineData (`part.inlineData.data`), decode it via `Buffer.from(imageData, "base64")` and write to `{project_root}/public/uploads/mindmaps/` using `fs.writeFileSync`.
5. Return the local relative URL to the main .NET system along with the rest of the chapter data.

## Frontend (React/Next.js) Development

1. Admin Panel: Introduce an image upload component inside the Teacher Settings or Lesson Configuration screens.
2. Student Dashboard: Update `GetLessonCockpitQuery` in backend to serve the `MindmapImageUrl` property.
3. Student Video Player: Implement `framer-motion` overlays inside `SecureVideoPlayer.tsx` to briefly flash the mind map at the end of a chapter.
4. Student Video Player: Add a persistent section just below the video player (or under `LessonCarousel.tsx`) to display the active chapter's mind map using an `<img>` tag (ensuring no `next/image` in motion containers per constitution).
