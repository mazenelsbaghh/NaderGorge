# Quickstart: Two-Phase Mindmap Generation

This quickstart guides developers through implementing the decoupling of AI Video Analysis from the actual Mind Map generation.

## Process
1. **Remove Coupling**: In `AnalyzeVideoAudioCommand`, stop dispatching mind map jobs or initiating generation logic immediately upon extracting chapters.
2. **Setup BullMQ**: Add the new Redis queue `generate-chapter-mindmaps` locally in `worker/src/index.ts`.
3. **Establish Backend Database Track**: Add `IsProcessingMindmaps` boolean status to `LessonVideo` in EF Core and create a Migration.
4. **Build Endpoint**: Add the `POST /api/Admin/Content/Lessons/{lessonId}/Videos/{videoId}/GenerateMindmaps` endpoint and MediatR command to submit the requested generation task.
5. **Update Frontend UI**: Implement the visual trigger mechanism in `AdminLessonVideoList` and properly surface the `IsProcessingMindmaps` lock status based on API responses. 

By executing these phases sequentially, the system achieves a mature, decoupled configuration that allows human intervention before costly AI mind map generations occur.
