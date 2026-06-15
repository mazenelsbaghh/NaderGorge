# Frontend Master Plan

**Last Updated**: 2026-06-15

---

## Active Plans

### Comprehensive Audit Remediation (2026-06-15)
- [x] Implement frontend-wide HTML sanitizer `sanitizeRichHtml` using DOMPurify/custom rules and apply it to essay/grading renderers.
- [x] Align profile completion form fields to submit backend-compatible `District` and `SchoolName` values instead of legacy `City`/`School`.
- [x] Create style-agnostic accessible `AccessibleDialog` container supporting focus trap containment, Escape key, backdrop click, and reduced motion.
- [x] Migrate `ProfileCompletionModal` to the new `AccessibleDialog` component.
- [x] Optimize SignalR chat connection to keep socket alive and reuse connection context on room switching using React callback refs.

### Admin & Teacher Content Page Dropdown Fix (2026-06-15)
- [x] Replace native select elements with custom Dropdown component on the Admin Content and Teacher Content pages to fix macOS Chrome RTL rendering bug.

### Real-time Platform Speed & Sync (2026-06-11)
- [x] Create universal `usePlatformEvents` custom React hook wrapping `@microsoft/signalr` to manage unified real-time event subscriptions (NotificationCreated, BalanceChanged, LessonPublished, VideoReady, ResourceReady, AiJobProgress).
- [x] Integrate `usePlatformEvents` in `StudentShellChrome.tsx` to dynamically sync balance and notifications count without page refreshes.
- [x] Expose `clearPackagesCache` in `content-service.ts` to clear API cache when new lessons or content updates are detected.
- [x] Integrate `usePlatformEvents` in `TermDetailPageClient.tsx`'s `SectionLessons` to refetch lessons lists instantly upon `LessonPublished` SignalR notifications.
- [x] Integrate `usePlatformEvents` in `LessonDetailPageClient.tsx` to automatically reload lesson videos and resources in real-time when `VideoReady` or `ResourceReady` events arrive.
- [x] Integrate `usePlatformEvents` in `AIMonitorPageClient.tsx` to receive `AiJobProgress` updates dynamically and slow down the polling interval to 30s to save database resources.

### Teacher Image WebP Conversion (2026-06-11)
- [x] Add base64 MIME type detection helper in `frontend/src/utils/image-compressor.ts`.
- [x] Rename teacher profile image file extension to `.webp` during upload in `AdminTeachersPageClient.tsx`.
- [x] Rename teacher AI photo file extension to `.webp` during upload in `AdminTeachersPageClient.tsx`.
- [x] Rename teacher profile image and AI photo file extensions to `.webp` in `TeacherProfilePageClient.tsx`.

### Performance Audit Remediation (2026-06-11)
- [x] Optimize `frontend/src/app/layout.tsx` to remove `force-dynamic` and `headers()`.
- [x] Add inline head script in root layout to set `data-massar-surface` dynamically from `window.location.host` without making layout dynamic.
- [x] Reduce font weights loaded for Tajawal and Montserrat in root layout.
- [x] Convert read-heavy pages (like student dashboard `app/student/page.tsx`, `/about`, `/faq`) to Next.js Server Components.
- [x] Remove global `framer-motion` page transition wrapper from `frontend/src/app/template.tsx` and replace with pure CSS transitions.
- [x] Integrate Axios service layer call to `/api/student/shell-bootstrap` to fetch student balance, notifications, and gamification in one request.
- [x] Refactor `StudentShellChrome.tsx`, `SidebarBalance.tsx`, and `SidebarGamification.tsx` to prevent waterfall requests on navigation.
- [x] Optimize and compress all logo SVGs under `frontend/public/images/` to under 50KB.


### Surface Login and Access Contract (2026-06-09)
- [ ] Create Next.js middleware `frontend/src/middleware.ts` to execute proxy boundaries.
- [ ] Configure local dev port detection and cross-surface path restrictions (rewrite to 404) in `frontend/src/packages/surface-runtime/config.ts` and `frontend/src/proxy.ts`.
- [ ] Add branded 404 error page at `frontend/src/app/not-found.tsx`.
- [ ] Customize login titles and subtitles dynamically by surface in `frontend/src/app/(public)/login/page.tsx`.
- [ ] Implement relative `returnUrl` validation in `LoginForm.tsx` and `login/page.tsx`.
- [ ] Update boundary assertions in `scripts/verify-surface-separation.mjs`.

### Role Pages and Permissions Completion (2026-06-09)
- [x] Create dedicated teacher activity dashboard `/teacher/activity` rendering active student progress grids, watch stats, and alerts.
- [x] Create student profile page `/student/profile` presenting personal details, device status, and profile update forms.
- [x] Create student notifications page `/student/notifications` displaying in-app notification feeds.
- [x] Update `StudentShellChrome.tsx` layout to include profile/notifications links, count unread notifications, and render notification count badges/dots.

### Domain and Docker Isolation Finalization (2026-06-09)
- [x] Configure fallback domain default to `massar-academy.net` in `frontend/src/packages/surface-runtime/config.ts`.
- [x] Set `data-massar-surface` attribute on the `<html>` element in `frontend/src/app/layout.tsx`.

### Assistant Portal & Staff Surface (2026-06-09)
- [x] Secure the `/assistant/*` routes and run them under AssistantGuard layout shell.
- [x] Implement a responsive, Arabic-first sidebar/navbar in AssistantShellChrome containing Dashboard, Tasks, CRM, Chat, Attendance, Vacations, and Notifications.
- [x] Create Tasks view pages at tasks/page.tsx and tasks/[id]/page.tsx with status transitions and comment feeds.
- [x] Integrate Attendance and Vacation pages in assistant portal.
- [x] Add root redirection inside assistant portal to map / to /assistant/dashboard to avoid 404 errors.

### Custom Forms API Payload Alignment (2026-06-07)
- [x] Fix `updateAdminForm` in `frontend/src/services/forms-service.ts` to include the `id` field inside the request body to resolve the 400 Bad Request ID mismatch.
- [x] Fix `updateSubmissionStatus` in `frontend/src/services/forms-service.ts` to include the `submissionId` field inside the request body to resolve the 400 Bad Request ID mismatch.
- [x] Use `resolveMediaUrl` to prevent broken cover image previews in forms by resolving image paths relative to the correct public domain.
- [x] Introduce `coverImageError` state in form creation (`new/page.tsx`) and form editing (`edit/page.tsx`) to catch failed image loads, unmount the failing img tag, and prevent infinite toast loops when images fail to load.

### Landing Page Dark Mode & Auto-Swiper Refinements (2026-06-06)
- [x] Update `CircularGallerySection.tsx` to programmatically animate the student stack card's dragX to simulate manual dragging.
- [x] Register color variables in Tailwind `@theme inline` in `globals.css` and transition hardcoded text color hexes to tailwind variables.
- [x] Connect `useAdminTheme` hook to `HeroSection.tsx` and `LandingFooter.tsx` to conditionally toggle dark images and overlays.
- [x] Update `resizable-navbar.tsx` to invert the logo mark in dark mode.

### Landing Page Overdrive: 3D Swiper Stack & Kinetic Reveals (2026-06-06)
- [x] Refactor gallery grids into draggable 3D stack cards and infinite auto-scroll row layout.
- [x] Connect kinetic typography bouncy entries in `HeroSection.tsx`.
- [x] Add auto-swipe cycle timer with exit animation to the student 3D Card Stack Swiper.

### Landing Page Overdrive: Cinematic Reveals & Canvas Particles (2026-06-06)
- [x] Implement interactive canvas component `ScholarlyParticles.tsx` for ambient animations.
- [x] Connect `home.tsx` and integrate all scroll-driven visuals and entrance staggering with Framer Motion.

### Student Forgot Password Flow (2026-06-04)
- [x] Create `/forgot-password` public route in `frontend/src/app/(public)/forgot-password/page.tsx`
- [x] Implement two-step wizard form:
  - **Step 1 (Verification)**: Fields for phone number, date of birth, governorate (Egypt's 27), and district (dynamic dropdown).
  - **Step 2 (Reset)**: Fields for new password and confirm password.
- [x] Integrate with `authService.verifyResetFields` and `authService.resetPassword`.
- [x] Apply Cairo typography, responsive layouts, RTL alignment, and the premium "Editorial Scholar" theme.

---

## History
- **2026-06-15**: Completed frontend security audit remediation: HTML sanitization in essay feeds, district/schoolName alignment in profile completions, shared accessible dialog wrapper, and SignalR socket connection reuse on room switches.
- **2026-06-15**: Fixed native select rendering bug on macOS Chrome (RTL) by replacing selects with the custom Dropdown component on Admin Content and Teacher Content pages.
- **2026-06-06**: Upgraded Landing Page Overdrive with auto-cycling 3D Card Stack Swiper and continuous horizontal marquee animations.
- **2026-06-06**: Completed Landing Page Overdrive visual layout and Framer Motion integration.
- - Initialized frontend master plan directory.
