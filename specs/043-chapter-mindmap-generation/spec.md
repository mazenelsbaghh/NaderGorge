# Feature Specification: Chapter Mindmap Generation

**Feature Branch**: `043-chapter-mindmap-generation`  
**Created**: 2026-04-01  
**Status**: Draft  
**Input**: User description: "عايز يعمل ب https://ai.google.dev/gemini-api/docs/image-generation للكل قسم خريطه ذهنيه ليه واكون بس رافع صور المدرس فحته و ياختدها عطول ويحطها عايز صور المدرس تبقي كراكتير و تبقي خريطه ذهنيه لكل فصل بالعربي واستخدم ده @[/prompt-engineering] علاشن تعمل برومت احلي"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Upload Teacher Reference Photos (Priority: P1)

As an administrator or teacher, I want to upload reference photos of myself, so that the AI can use them to generate personalized mind maps featuring my likeness.

**Why this priority**: Without reference photos, the AI cannot generate the personalized caricature, which is a core requirement of the feature.

**Independent Test**: Can be fully tested by navigating to the teacher profile or lesson settings, uploading an image, and verifying it is saved and accessible by the backend worker.

**Acceptance Scenarios**:

1. **Given** an admin is configuring a lesson or teacher profile, **When** they upload 1 or more photos, **Then** the photos are securely saved and their URIs/IDs are recorded in the database.
2. **Given** previously uploaded photos, **When** a new video processing job starts, **Then** the system automatically fetches these photos to use as negative/positive prompts for image generation.

---

### User Story 2 - Automated Mind Map Generation per Chapter (Priority: P1)

As a content creator, I want the system to automatically generate a mind map image for each chapter using the Gemini Image Generation API, so that students have a visual summary of the chapter's Arabic concepts featuring my caricature.

**Why this priority**: This is the core engine of the feature request, bridging the existing chaptering system with the new Gemini Imagen capabilities.

**Independent Test**: Can be fully tested by triggering a lesson analysis; the system should successfully call the Gemini API and output an image URL for each generated chapter.

**Acceptance Scenarios**:

1. **Given** a successfully extracted list of video chapters and teacher reference photos, **When** the AI generation pipeline runs, **Then** a distinct prompt is engineered for each chapter's summary to generate a mind map.
2. **Given** the generated prompt, **When** the Gemini API is called, **Then** it returns an Arabic text-integrated mind map image featuring a caricature of the teacher.

---

### User Story 3 - Displaying Mind Maps to Students (Priority: P2)

As a student, I want to see the mind map associated with the current chapter I am watching, so that I can visually grasp the key Arabic concepts being explained.

**Why this priority**: Completes the feature's lifecycle by presenting the generated value to the end user.

**Independent Test**: Can be fully tested by opening a lesson with generated mind maps and verifying the images display correctly alongside or within the video player.

**Acceptance Scenarios**:

1. **Given** a student watching a lesson, **When** a new chapter begins, **Then** the corresponding mind map image becomes viewable.

### Edge Cases

- What happens when the Gemini API rejects the image generation due to safety filters or prompt complexity?
- How does the system handle Arabic text rendering issues commonly found in AI image generation? (If AI image generation fails at rendering Arabic cleanly, how do we fallback? Generate just the background and overlay text over the image using HTML?)
- What happens if the admin attempts to generate chapters without having uploaded any teacher reference photos?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow administrators to upload reference photos of the teacher.
- **FR-002**: System MUST integrate with the Gemini Image Generation API.
- **FR-003**: System MUST construct an advanced, few-shot prompt using [Prompt Engineering Patterns] to ensure the output is an educational mind map.
- **FR-004**: System MUST instruct the AI to render a caricature avatar of the teacher based on the provided reference photos within the mind map.
- **FR-005**: System MUST instruct the AI to use Arabic text strictly for the mind map nodes and associations.
- **FR-006**: System MUST link the generated mind map image URL/File to its corresponding `Chapter` record in the database.
- **FR-007**: System MUST display the generated mind map to the student in two locations: 
  1. **Inside the Video Player (Overlay):** Briefly displayed as an interstitial or overlay precisely when a chapter finishes.
  2. **Below the Video Player (Persistent):** A dedicated section below the video player that persistently shows the mind map corresponding to the currently active chapter.

### Key Entities

- **TeacherPhoto**: Stores reference images used for caricature generation.
- **ChapterImage**: An attribute or related entity for the `LessonChapter` storing the generated mind map's URL.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of generated chapters have a corresponding generated image attempt.
- **SC-002**: At least 80% of generated mind maps accurately reflect the Arabic summary text without hallucinating unrelated concepts.
- **SC-003**: The teacher's caricature is visually consistent with the uploaded reference photos in at least 90% of generations.
- **SC-004**: Generated images are stored and served with a latency of less than 2 seconds when requested by the student interface.

## Assumptions

- The Gemini Image generation API models available support the input of a reference structural image or reference face.
- Arabic text rendering in Gemini Imagen generates legible text and not "pseudo-text" artifacts. (If it does, we may need to explore rendering SVG maps dynamically instead).
- The existing background worker architecture (BullMQ) has sufficient retry logic to handle rate limits from the Gemini Image API.
