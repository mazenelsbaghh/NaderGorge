# Tasks: AI Live Support Actions & Verification

**Input**: Design documents from `/specs/145-ai-live-support-actions/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/api.md
**Expected Result**: All automated and manual verification passes.


## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) complete
- [x] Phase 2: Arabic Clarification (`speckit-clarify`) complete
- [x] Phase 3: Technical Planning (`speckit-plan`) complete
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`) complete

---

## Phase 1: Foundational (Worker & Schema Updates)

**Purpose**: Update the Node.js worker and Gemini decision schema to support action, verification, and registration decision types.

- [x] T005 [P] Update `liveSupportDecisionSchema` in `worker/src/services/geminiService.ts` to support all 5 decision types (`reply`, `propose_action`, `request_verification`, `propose_account_creation`, `handoff`).
- [x] T006 [P] Update `generateLiveSupportReply` in `worker/src/services/geminiService.ts` to parse the new decision formats and output them to the C# callback.

---

## Phase 2: User Story 1 - Confirmed Actions & Context (Priority: P1) 🎯 MVP

**Goal**: Students can request allowed actions, AI proposes them via a confirmation card, and backend executes them after confirmation.

**Independent Test**: Verify lesson unlocks when student confirms the card.

- [x] T007 [P] [US1] Implement dynamic student profile context formatting in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs` which serializes allowed readable data keys into a structured document.
- [x] T008 [P] [US1] Implement dynamic system instructions building in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs` by appending instructions and argument schemas for only the allowed action keys.
- [x] T009 [P] [US1] Extend `CompleteAITurnAsync` in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs` to handle `propose_action` decision, validate action permissions, insert `LiveSupportAIPendingAction` and broadcast via SignalR.
- [x] T010 [P] [US1] Implement action confirmation endpoints `ConfirmPendingActionAsync` and `CancelPendingActionAsync` in `backend/src/NaderGorge.API/Controllers/LiveSupportParticipantController.cs` and `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs`.
- [x] T011 [P] [US1] Create the frontend interactive card component `AIPendingActionCard.tsx` in `frontend/src/components/live-support/participant/` to render action details with confirm/cancel buttons.
- [x] T012 [P] [US1] Wire action confirmation and cancellation handlers in `frontend/src/components/live-support/ParticipantConversation.tsx` and `frontend/src/services/live-support-service.ts`.
- [x] T013 [P] [US1] Add unit tests in `backend/tests/NaderGorge.Application.Tests/LiveSupport/ParticipantSessionTests.cs` to verify action proposals, validation, execution, and expiration constraints.

---

## Phase 3: User Story 2 - Confirmed Handoff (Priority: P1)

**Goal**: AI proposes human handoffs; student gets a confirmation card to accept or decline.

**Independent Test**: Verify conversation remains active with AI when student rejects the handoff card.

- [x] T014 [P] [US2] Modify `CompleteAITurnAsync` in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs` so normal AI handoff triggers a handoff proposal without transitioning the conversation to human support queue immediately.
- [x] T015 [P] [US2] Implement the `ConfirmHandoffAsync` and `CancelHandoffAsync` endpoints in `backend/src/NaderGorge.API/Controllers/LiveSupportParticipantController.cs` and `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs`.
- [x] T016 [P] [US2] Create the frontend interactive card component `AIHandoffConfirmation.tsx` in `frontend/src/components/live-support/participant/` to show the handoff card to the student.
- [x] T017 [P] [US2] Update `frontend/src/components/live-support/ParticipantConversation.tsx` and `frontend/src/services/live-support-service.ts` to wire handoff confirmation/rejection.
- [x] T018 [P] [US2] Add unit tests in `backend/tests/NaderGorge.Application.Tests/LiveSupport/ParticipantSessionTests.cs` verifying handoff confirm and cancel behaviors.

---

## Phase 4: User Story 3 - Guest Verification & Registration (Priority: P2)

**Goal**: Guests can verify existing accounts through challenge questions or register a new account via a secure form.

**Independent Test**: Guest registers account, verify student profile is created and linked.

- [x] T019 [P] [US3] Implement guest verification endpoints (lookup, answer challenge) in `backend/src/NaderGorge.API/Controllers/LiveSupportParticipantController.cs` and `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs` using the Matched Candidate lookup and normalization.
- [x] T020 [P] [US3] Implement secure registration endpoint `ConfirmRegistrationProposalAsync` in `backend/src/NaderGorge.API/Controllers/LiveSupportParticipantController.cs` which validates inputs, creates a student profile, and links the conversation.
- [x] T021 [x] [US3] Create `AIGuestVerification.tsx` and `AISecureRegistrationForm.tsx` components in `frontend/src/components/live-support/participant/` to render verification inputs and the registration form.
- [x] T022 [x] [US3] Update `frontend/src/components/live-support/ParticipantConversation.tsx` to display guest registration and verification flows.
- [x] T023 [x] [US3] Add unit tests verifying guest verification matching, attempt count lockout, and registration validation.

---

## Phase 5: Polish & Quality Verification (Mandatory Order)

**Purpose**: Review code quality, run code and test guards, compile, and execute tests.

- [x] T024 Perform deep critique of C# backend controllers, worker service, and student widget components, fixing any code smells or layer violations.
- [x] T025 Run `clean-code-guard` against all changed C# files and TypeScript/Node worker files.
- [x] T026 Run `test-guard` against all changed or newly created test files.
- [x] T027 Run all backend tests to verify 100% test success: `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj`.
- [x] T028 Run frontend compile validation: `(cd frontend && npx tsc --noEmit)`.
- [x] T029 Build production bundle of frontend and backend and run final feature tests: `npm run build` (frontend), `dotnet build` (backend).

