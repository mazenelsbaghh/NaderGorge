# Feature Specification: AI Agent Chaptering Workflow

**Feature Branch**: `039-ai-agent-chaptering`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "حولوا ل ai agent علشان الوقت و علشان المراحل" (Convert Video AI to Multi-Stage Agent for time management and phased execution)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Multi-Stage Progress Tracking (Priority: P1)

As an administrator uploading long educational videos, I want the AI chaptering process to be divided into clear, visible stages (Downloading, Extracting Audio, AI Analysis, Formatting Subtitles), so that I know exactly what the system is currently doing during the 10+ minute wait time rather than seeing a generic "Processing" state.

**Why this priority**: Long-running synchronous processes cause anxiety and confusion. Providing granular visibility into the AI's current operational stage reassures the user that the system hasn't crashed.

**Independent Test**: Can be fully tested by submitting a new lesson video to the AI extraction queue and observing the admin dashboard update its state text dynamically through 4-5 distinct phases.

**Acceptance Scenarios**:

1. **Given** a newly triggered AI analysis, **When** the worker picks up the job, **Then** the UI shows "Downloading Video...".
2. **Given** the video is downloaded, **When** the system starts processing audio, **Then** the UI updates to "Extracting Audio Track...".
3. **Given** audio is sent to the LLM, **When** waiting for the long API response, **Then** the UI shows "AI is generating chapters (This may take several minutes)...".

---

### User Story 2 - Resilient Stage-Level Retries (Priority: P2)

As an administrator managing AI jobs, I want the system to be fault-tolerant at the stage level, so that if the AI API times out after 7 minutes, I don't have to re-download and re-extract the original 2GB video file when I hit "Retry".

**Why this priority**: Saves significant bandwidth and processing time. If the backend fails to connect to Gemini, we shouldn't lose the already extracted audio file.

**Independent Test**: Can be fully tested by artificially causing a network timeout during the AI generation phase, then pressing "Retry" on the dashboard and verifying the system skips the extraction phase and immediately resumes AI generation.

**Acceptance Scenarios**:

1. **Given** a job that successfully extracted audio but failed at the AI API call, **When** the admin clicks "Retry", **Then** the agent resumes directly from the "AI Analysis" stage using the cached audio file.
2. **Given** a job that fails during the webhook sync phase, **When** retrying, **Then** it skips AI generation entirely and only attempts to resend the JSON payload to the backend.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST decompose the monolithic `analyzeVideoChapters` function into a stateful, directed graph or orchestrated workflow (e.g., LangGraph, CrewAI, or BullMQ Flow).
- **FR-002**: System MUST persist the output of each stage (temporary audio paths, raw JSON responses) to avoid redundant work on retries.
- **FR-003**: System MUST update the BullMQ job progress and state message at the start and end of every distinct stage.
- **FR-004**: System MUST handle cleanup of temporary artifacts (e.g., `.mp3` files) only when the *entire* workflow completes successfully or is explicitly abandoned/canceled.
- **FR-005**: The Admin Dashboard MUST read the detailed stage names from the progress API and display them in Arabic to the user.

### Key Entities *(include if feature involves data)*

- **AIAgentWorkflowState**: A persisted object representing the current execution context (audio file path, raw API result, generated SRT, final Chapters JSON).
- **VideoAnalysisJob**: Expanded queue job definition to support steps: `[DOWNLOAD -> EXTRACT -> ANALYZE -> SYNC]`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A 1-hour long video can be processed reliably across multiple independent worker stages without hitting single-function timeout limits.
- **SC-002**: If the AI processing step fails, clicking "Retry" successfully recovers within 10 seconds of queue time by utilizing cached audio, reducing retry overhead by 100%.
- **SC-003**: Administrators receive at least 4 distinct progress updates during the lifecycle of the AI operation in the UI.

## Assumptions

- We will utilize the existing BullMQ infrastructure, migrating from a single job to a "BullMQ Flow" or a state-machine based single job.
- The `gemini-2.5-flash` model will remain the core AI agent utilized in the `ANALYZE` stage.
- Local storage (`.tmp/`) is sufficient for caching intermediate stage artifacts (audio, raw JSON). Advanced distributed caching (S3) is out of scope for v1 of the agent workflow.
