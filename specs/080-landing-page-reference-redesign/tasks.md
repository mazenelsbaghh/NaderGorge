# Tasks: Landing Page Reference Redesign

**Input**: Design documents from `/specs/080-landing-page-reference-redesign/`
**Prerequisites**: `plan.md`, `spec.md`

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed
- [x] Phase 2: Technical Planning (`speckit-plan`) completed
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed

## Phase 1: Asset And Token Setup

- [x] T001 Copy `/Users/mazenelsbagh/Downloads/first.png` to `frontend/public/images/landing-hero.png`.
- [x] T002 In `frontend/src/app/globals.css`, replace landing decorative gold-tinted backgrounds with white, soft gray, deep navy, and teal landing section utilities.

## Phase 2: Landing Data

- [x] T003 In `frontend/src/components/landing/data.ts`, define navigation links for `#courses`, `#teachers`, `#about-platform`, and `#contact`.
- [x] T004 In `frontend/src/components/landing/data.ts`, define typed arrays for top students, teachers, testimonials, and educational tracks.

## Phase 3: Section Components

- [x] T005 In `frontend/src/components/landing/LandingNav.tsx`, implement the compact reference-style header with logo, nav links, login, and start CTA.
- [x] T006 In `frontend/src/components/landing/HeroSection.tsx`, replace the current pharaoh hero with copy-left and image-right reference layout using `/images/landing-hero.png`.
- [x] T007 In `frontend/src/components/landing/CircularGallerySection.tsx`, replace the circular gallery with top students and teachers sections.
- [x] T008 In `frontend/src/components/landing/TestimonialsSection.tsx`, replace the vertical animated feed with a compact testimonial card carousel-style row.
- [x] T009 In `frontend/src/components/landing/LandingFooter.tsx`, implement the courses/tracks section plus dark final CTA and footer.
- [x] T010 In `frontend/src/app/page.tsx`, ensure the landing renders sections in the required order.

## Phase 4: Verification

- [x] T011 Run `npm run lint` from `frontend/`.
- [x] T012 Verify the rendered homepage visually in browser at desktop and mobile widths.

## Warnings and Issues / تحذيرات ومشاكل

- [x] W001 Replace raw `<img>` logo in `frontend/src/components/landing/LandingNav.tsx` with `next/image`.
- [x] W002 Replace raw `<img>` logo in `frontend/src/components/landing/LandingFooter.tsx` with `next/image`.
- [x] W003 Replace raw `<img>` logo in `frontend/src/components/ui/resizable-navbar.tsx` with `next/image`.
- [x] W004 Replace unavailable `lucide-react` social icon imports in `frontend/src/components/landing/LandingFooter.tsx`.
- [x] W005 Add eager loading for above-the-fold landing hero image.
- [x] W006 Preserve logo SVG aspect ratio for `next/image` sizing.

## Critique & Architectural Issues / مشاكل الانتقاد والبنية

- [x] C001 Make testimonials visible by default instead of depending on `whileInView` reveal.
- [x] C002 Restore the old animated landing navbar by using `GlobalNav` on `/` and updating its landing links.
