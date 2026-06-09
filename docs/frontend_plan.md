# Frontend Master Plan

**Last Updated**: 2026-06-09

---

## Active Plans

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
- **2026-06-06**: Upgraded Landing Page Overdrive with auto-cycling 3D Card Stack Swiper and continuous horizontal marquee animations.
- **2026-06-06**: Completed Landing Page Overdrive visual layout and Framer Motion integration.
- Initialized frontend master plan directory.
