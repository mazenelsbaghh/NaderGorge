# Feature Specification: Landing Page Reference Redesign

**Feature Branch**: `080-landing-page-reference-redesign`  
**Created**: 2026-06-06  
**Status**: Draft  
**Input**: User provided a six-screen landing page reference image, `first.png` as the required hero image, and a brand identity PDF. The requested direction is to match the reference shape closely, use the new Massar Academy white, deep navy, and teal identity across student-facing surfaces, and stop treating gold as the primary color.

## User Scenarios & Testing

### User Story 1 - Reference-Matched Landing Experience (Priority: P1)

Students and parents visiting the public homepage need to see a landing page that visually matches the provided reference: clean white surface, deep navy typography, teal actions, simple navigation, a large right-side hero image, student achievers, teachers, testimonials, educational tracks, and a dark final CTA/footer.

**Independent Test**: Load `/` on desktop and mobile and compare the page structure to the six reference frames.

### User Story 2 - New Brand Color Hierarchy (Priority: P1)

The user needs the new brand identity to be dominant: deep navy and teal are primary, white is the base surface, and gold is only a small highlight for ratings, medals, or the graduation tassel.

**Independent Test**: Inspect buttons, headings, cards, footer, and accents. No primary call to action uses gold.

### User Story 3 - Hero Image Integration (Priority: P1)

The homepage hero must use the provided `first.png` asset on the right side in desktop layouts and remain legible on mobile without cropping important visual content.

**Independent Test**: Verify `/images/landing-hero.png` renders in the hero and maintains aspect ratio across desktop and mobile.

## Requirements

- **FR-001**: The homepage MUST use the provided hero image as the main visual asset.
- **FR-002**: The homepage MUST contain sections matching the reference: hero, top students, teachers, testimonials, tracks, and final CTA/footer.
- **FR-003**: The landing design MUST use deep navy `#0A1D3D`, teal `#0E8F8F`, and white/soft gray surfaces as dominant colors.
- **FR-004**: Gold MUST be limited to secondary highlights such as stars and rank medals.
- **FR-005**: Navigation MUST include links for homepage, courses, teachers, about platform, and contact.
- **FR-006**: The layout MUST be responsive and preserve readable Arabic text at mobile widths.
- **FR-007**: The implementation MUST keep landing components small, typed, and isolated in `frontend/src/components/landing`.

## Success Criteria

- **SC-001**: `/` displays the provided hero image and all six reference-inspired content areas.
- **SC-002**: `npm run lint` succeeds for the frontend.
- **SC-003**: No landing text overflows at common mobile widths.
- **SC-004**: All primary CTAs use teal or deep navy, not gold.

## Edge Cases

- If the public stats API is unavailable, the landing page still renders with a sensible fallback.
- If a mobile viewport is narrow, card groups stack cleanly and action buttons remain at least 44px tall.
- If reduced motion is enabled, content remains visible and animations do not block rendering.
