# Implementation Tasks: Nested Content Profiles

**Branch**: `018-nested-content-profiles`  
**Spec**: [spec.md](./spec.md)  
**Plan**: [plan.md](./plan.md)  

## Implementation Strategy
- Build the backend foundational queries first to ensure API endpoints are ready for both stories.
- Build User Story 1 completely before User Story 2, so the hierarchy (Term -> Section -> Lesson) can be tested sequentially.

## Phases

### Phase 1: Setup
*(No pure application setup tasks required since the project is fully bootstrapped)*

### Phase 2: Foundational  
**Goal**: Prepare the read models and API endpoints required to fetch specific Term and Section metadata.

- [x] T001 [P] Create `GetTermByIdQuery` in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetTermByIdQuery.cs`.
- [x] T002 [P] Create `GetSectionByIdQuery` in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetSectionByIdQuery.cs`.
- [x] T003 Add `GetTermById` and `GetSectionById` endpoints to `backend/src/NaderGorge.API/Controllers/AdminController.cs`.
- [x] T004 [P] Add `getTermById` and `getSectionById` to `frontend/src/services/admin-service.ts`.

### Phase 3: User Story 1 - Navigate and Manage Sections within a Term (P1)
**Goal:** Allow admins to go to a Term's profile, view its details, and add sections to it.
**Independent Test:** Admin can navigate from packages to terms, click a term, see its profile, and add a section.

- [x] T005 [P] [US1] Create `SectionListManager` component in `frontend/src/components/admin/SectionListManager.tsx` fetching `contentService.getSections(termId)`.
- [x] T006 [P] [US1] Create `AddSectionForm` component in `frontend/src/components/admin/AddSectionForm.tsx` calling `adminService.createSection(termId, ...)`.
- [x] T007 [US1] Export new components from `frontend/src/components/admin/index.ts`.
- [x] T008 [US1] Create `TermProfilePage` page at `frontend/src/app/admin/content/terms/[id]/page.tsx` utilizing `React.use(props.params)` and rendering `SectionListManager` and `AddSectionForm`.
- [x] T009 [US1] Update `TermListManager` in `frontend/src/components/admin/TermListManager.tsx` to add "Eye" icon acting as navigate action to `/admin/content/terms/${term.id}`.

### Phase 4: User Story 2 - Navigate and Manage Lessons within a Section (P1)
**Goal:** Allow admins to go to a Section's profile, view its details, and add lessons to it.
**Independent Test:** Admin can navigate from term to sections, click a section, see its profile, and add a lesson.

- [x] T010 [P] [US2] Create `LessonListManager` component in `frontend/src/components/admin/LessonListManager.tsx` fetching `contentService.getLessons(sectionId)`.
- [x] T011 [P] [US2] Create `AddLessonForm` component in `frontend/src/components/admin/AddLessonForm.tsx` calling `adminService.createLesson(sectionId, ...)`.
- [x] T012 [US2] Export new components from `frontend/src/components/admin/index.ts`.
- [x] T013 [US2] Create `SectionProfilePage` page at `frontend/src/app/admin/content/sections/[id]/page.tsx` utilizing `React.use(props.params)` and rendering `LessonListManager` and `AddLessonForm`.
- [x] T014 [US2] Update `SectionListManager` in `frontend/src/components/admin/SectionListManager.tsx` to add "Eye" icon acting as navigate action to `/admin/content/sections/${section.id}`.

### Phase 5: Polish & Cross-Cutting Concerns

- [x] T015 Verify routing and Next.js Fast Refresh across all nested profiles (`terms/[id]`, `sections/[id]`) and test RTL styling conformity.

---

## Dependencies

- **Phase 2** (Foundational) must complete before **Phase 3** or **Phase 4**.
- `T007` and `T008` (US1 App integration) depend on `T005` and `T006`.
- `T012` and `T013` (US2 App integration) depend on `T010` and `T011`.
- `T009` (Linking Term to Section) can happen independent of US1 completion, but is most useful afterwards.

## Parallel Execution Opportunities

- After Phase 2 completes, developers/agents can build `SectionListManager` and `AddSectionForm` in parallel, as well as `LessonListManager` and `AddLessonForm` in parallel, since they are largely disjoint UI components querying the backend.
