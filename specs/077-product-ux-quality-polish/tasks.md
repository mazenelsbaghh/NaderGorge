# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

# Tasks: Product UX, Frontend Quality, and Polish

Target prompt: create the tasks file so that a cheaper llm model can implement without problems

## A. Mock Analytics and Trust

- [x] T001 In `frontend/src/components/admin/EntityOverviewDashboard.tsx`, remove the unused `Eye` import.
- [x] T002 In `frontend/src/components/admin/EntityOverviewDashboard.tsx`, make the dashboard render an explicit analytics empty state when no real stat values are passed.
- [x] T003 In `frontend/src/app/admin/content/packages/[id]/page.tsx`, remove `mockStats` and pass no analytics stats unless real API values exist.
- [x] T004 In `frontend/src/app/admin/content/sections/[id]/page.tsx`, remove `mockStats` and pass no analytics stats unless real API values exist.
- [x] T005 In `frontend/src/app/admin/content/lessons/[id]/page.tsx`, remove unused `LinkExamForm` and any mock analytics props.
- [x] T006 In `frontend/src/app/admin/content/terms/[id]/page.tsx`, remove `mockStats` and pass no analytics stats unless real API values exist.
- [x] T007 In `frontend/src/components/admin/AttachedExamViewer.tsx`, remove any `Math.random()` metrics and show real counts or an empty state.
- [x] T008 Run `rg -n "mockStats|Math\\.random" frontend/src` and remove production-visible mock/random analytics matches.

## B. Lint Cleanup, Unused Code

- [x] T009 In `frontend/src/app/(public)/forgot-password/page.tsx`, remove unused `motion`, `ArrowRight`, `LockKeyhole`, and `Sparkles` imports.
- [x] T010 In `frontend/src/app/about/page.tsx`, remove unused `BookOpenText`.
- [x] T011 In `frontend/src/app/admin/ai-monitor/page.tsx`, remove unused `hasChapters` and convert compatible `<img>` usage to `next/image`.
- [x] T012 In `frontend/src/app/admin/content/exams/[id]/add-question/page.tsx`, remove the unused `_af` parameter.
- [x] T013 In `frontend/src/app/admin/content/page.tsx`, remove unused `Trash2` and `ContentHierarchyPanel`.
- [x] T014 In `frontend/src/app/admin/content/sections/[id]/page.tsx`, remove unused `ChevronLeft` and `AdminStatCard`.
- [x] T015 In `frontend/src/app/admin/content/terms/[id]/page.tsx`, remove unused `AdminStatCard`.
- [x] T016 In `frontend/src/app/admin/questions/page.tsx`, remove unused `formatCompactNumber`.
- [x] T017 In `frontend/src/app/admin/users/[id]/page.tsx`, remove unused `Calendar` and `RotateCcw` imports.
- [x] T018 In `frontend/src/app/admin/users/[id]/page.tsx`, change intentionally unused caught `err` variables to `catch`.
- [x] T019 In `frontend/src/app/admin/users/page.tsx`, remove unused `PencilLine`, `Trash2`, and governorate setter state if not used.
- [x] T020 In `frontend/src/app/forms/[slug]/page.tsx`, remove unused `router`.
- [x] T021 In `frontend/src/components/admin/AddLessonForm.tsx`, change unused caught `error` to `catch`.
- [x] T022 In `frontend/src/components/admin/AddResourceForm.tsx`, change unused caught `error` to `catch`.
- [x] T023 In `frontend/src/components/admin/AddSectionForm.tsx`, change unused caught `error` to `catch`.
- [x] T024 In `frontend/src/components/admin/AddTermForm.tsx`, change unused caught `error` to `catch`.
- [x] T025 In `frontend/src/components/admin/AddVideoForm.tsx`, remove unused `PlaySquare` and change unused caught `error` to `catch`.
- [x] T026 In `frontend/src/components/admin/AdminTeacherPhotoUpload.tsx`, convert compatible `<img>` usage to `next/image`.
- [x] T027 In `frontend/src/components/admin/EssayGradingView.tsx`, change unused caught `err` variables to `catch`.
- [x] T028 In `frontend/src/components/admin/LessonHomeworkList.tsx`, remove unused `CheckCircle2`, `Circle`, and `toast`.
- [x] T029 In `frontend/src/components/admin/LessonResourceList.tsx`, remove unused `toast`.
- [x] T030 In `frontend/src/components/admin/LessonVideoList.tsx`, convert compatible `<img>` usage to `next/image` and remove unused `lessonId` parameter.
- [x] T031 In `frontend/src/components/admin/LinkExamForm.tsx`, change unused caught `error` variables to `catch`.
- [x] T032 In `frontend/src/components/admin/PackageDetailsForm.tsx`, change unused caught `error` to `catch`.
- [x] T033 In `frontend/src/components/admin/UserRoleDropdown.tsx`, change unused caught `e` to `catch`.
- [x] T034 In `frontend/src/components/admin/content/HomeworkTabEditor.tsx`, remove unused `AnimatePresence` and change unused caught `e` to `catch`.
- [x] T035 In `frontend/src/components/codes/QrScanner.tsx`, change unused caught `e` to `catch`.
- [x] T036 In `frontend/src/components/landing/TestimonialsSection.tsx`, convert compatible `<img>` usage to `next/image`.
- [x] T037 In `frontend/src/components/landing/data.ts`, remove unused lucide icon imports.
- [x] T038 In `frontend/src/components/student-pages/CodeRedemptionShowcase.tsx`, remove unused `KeyRound`.
- [x] T039 In `frontend/src/components/student-pages/PackagesOverview.tsx`, remove unused `BookCopy`.
- [x] T040 In `frontend/src/components/student/CommunityPostComments.tsx`, remove unused `setIsOpen` binding if not needed.
- [x] T041 In `frontend/src/components/ui/SplitText.tsx`, remove unused `_` callback parameters.
- [x] T042 In `frontend/src/components/ui/UserAvatar.tsx`, remove or use the unused `role` prop binding.
- [x] T043 In `frontend/src/components/ui/resizable-navbar.tsx`, remove unused `useRef` import and unused `onClose` prop binding.
- [x] T044 In `frontend/src/components/video/ChapterList.tsx`, remove unused `Play`.
- [x] T045 In `frontend/src/hooks/useRootOverscrollBackground.ts`, remove unused `CSSProperties`.
- [x] T046 In `frontend/src/utils/dom-shield.ts`, remove unused `iframe` callback parameter.
- [x] T047 In `frontend/src/utils/whatsapp-utils.ts`, remove or use unused `debounceMs`.
- [x] T048 In frontend test files, remove unused `page`, `seedRes`, and `config` variables.

## C. Hook Dependency Cleanup

- [x] T049 In `frontend/src/app/admin/content/exams/[id]/dashboard/page.tsx`, wrap `loadDashboard` in `useCallback` and include it in `useEffect`.
- [x] T050 In `frontend/src/app/admin/forms/[id]/submissions/page.tsx`, wrap `loadData` in `useCallback` and include it in `useEffect`.
- [x] T051 In `frontend/src/app/admin/users/[id]/page.tsx`, wrap `fetchStudent` in `useCallback` and include it in `useEffect`.
- [x] T052 In `frontend/src/components/admin/LessonListManager.tsx`, wrap `loadLessons` in `useCallback` and include it in `useEffect`.
- [x] T053 In `frontend/src/components/admin/SectionListManager.tsx`, wrap `loadSections` in `useCallback`, include it in `useEffect`, and remove unused delete state/imports.
- [x] T054 In `frontend/src/components/admin/TermListManager.tsx`, wrap `loadTerms` in `useCallback`, include it in `useEffect`, and change unused caught errors to `catch`.
- [x] T055 In `frontend/src/components/admin/content/HomeworkTabEditor.tsx`, wrap `loadHomework` in `useCallback` and include it in `useEffect`.
- [x] T056 In `frontend/src/components/assistant/AssistantTaskBoard.tsx`, wrap `fetchTasks` in `useCallback` and include it in `useEffect`.
- [x] T057 In `frontend/src/components/ui/animated-theme-toggler.tsx`, include `checked` in the `useCallback` dependency list or refactor safely.
- [x] T058 In `frontend/src/components/video/PlayerControls.tsx`, memoize `displayChapters` before using it as a dependency.

## D. Secure Video Player

- [x] T059 In `frontend/src/components/video/SecureVideoPlayer.tsx`, remove unused `ImageIcon` and `InlineLoader` imports.
- [x] T060 In `frontend/src/components/video/SecureVideoPlayer.tsx`, either wire `onEnded` to the video element or remove it from props if unused.
- [x] T061 In `frontend/src/components/video/SecureVideoPlayer.tsx`, remove unused quality state and handler if real quality switching is not implemented.
- [x] T062 In `frontend/src/components/video/SecureVideoPlayer.tsx`, remove unused `user` state if it is not rendered or used for authorization.

## E. Copy, Brand Drift, and Heavy UI

- [x] T063 Run `rg -n "basma|bsma|acadmy|AI Monitor|Worker|BullMQ" frontend/src` and replace visible old brand or technical copy with Arabic user-facing copy where appropriate.
- [x] T064 Search imports for `circular-gallery`, `feature-carousel`, `ripple-grid`, and `resizable-navbar`; ensure none are unnecessarily imported into student first screens.
- [x] T065 Run `git ls-files | rg '(^|/)(node_modules|\\.next|dist|bin|obj)(/|$)'` and confirm generated artifacts are not tracked.

## F. UX and Responsive Verification

- [x] T066 Review `frontend/src/components/layout/StudentShellChrome.tsx` for study-focused navigation and stable mobile touch targets.
- [x] T067 Review `frontend/src/app/student/page.tsx` to keep next action visible in the first mobile viewport.
- [x] T068 Review `frontend/src/app/student/packages/page.tsx` and `frontend/src/app/student/mistakes/page.tsx` for clear empty/locked states.
- [x] T069 Review `frontend/src/app/admin/ai-monitor/page.tsx` for user-facing status labels and action clarity.
- [x] T070 Start the local frontend dev server and inspect key student/admin routes in mobile and desktop viewports.

## G. Final Verification

- [x] T071 Run `cd frontend && npm run build`.
- [x] T072 Run `cd frontend && npm run lint`.
- [x] T073 Run `rg -n "mockStats|Math\\.random|basma|bsma|acadmy" frontend/src || true` and verify no production-visible old/mock matches remain.
- [x] T074 Create `specs/077-product-ux-quality-polish/final-report.md` with implementation log, critique findings, verification results, and remaining justified exceptions if any.

## Critique & Architectural Issues / مشاكل الانتقاد والبنية

- [x] T075 Add `frontend/public/noise.svg` because Playwright smoke exposed a missing decorative asset referenced by student surfaces.
