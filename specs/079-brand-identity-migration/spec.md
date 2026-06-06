# Feature Specification: Brand Identity Migration (Massar Academy / مسار أكاديمي)

**Feature Branch**: `079-brand-identity-migration`  
**Created**: 2026-06-05  
**Status**: Draft  
**Input**: User description: "Migration to the new brand identity, name (Massar Academy / مسار أكاديمي), and logo."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Rebrand Logos & Icons (Priority: P1)

The user wants the visual logo and icons representing the platform to be fully updated to the new brand logo to establish immediate visual recognition.

**Why this priority**: Highly critical for brand alignment and user recognition. The logo is the primary identifier of the platform.

**Independent Test**: Can be fully tested by loading the website's landing page, main dashboard, and checking the tab favicon to verify the correct SVG logo is displayed.

**Acceptance Scenarios**:

1. **Given** a user navigates to the application, **When** the header or footer renders, **Then** the new "Massar Academy" logo (Arabic: مسار أكاديمي / English: Massar Academy) is displayed instead of the old logo.
2. **Given** the app is open in a browser tab, **When** looking at the tab, **Then** the new favicon is displayed.

---

### User Story 2 - Name & Copy Update (Priority: P1)

The user wants all text references to the platform's old name ("Nader Gorge") to be completely replaced with "Massar Academy" (in English contexts) and "مسار أكاديمي" (in Arabic contexts).

**Why this priority**: Crucial for legal, professional, and brand consistency. Having mixed brand names creates user confusion.

**Independent Test**: Can be tested by searching the entire client-side interface, footer, email templates, and page metadata for occurrences of the old name.

**Acceptance Scenarios**:

1. **Given** a user visits any page, **When** reading the page title or meta tags, **Then** the title displays "Massar Academy" or "مسار أكاديمي".
2. **Given** a user receives a system notification or email, **When** reviewing the sender name and signature, **Then** it references "Massar Academy" / "مسار أكاديمي".

---

### User Story 3 - Design System Colors & Font Migration (Priority: P2)

The user wants the colors and typography of the platform to be updated to match the new brand identity specifications.

**Why this priority**: Important for the overall aesthetic unity and visual brand experience of the application.

**Independent Test**: Can be tested by inspecting the CSS classes, Tailwind styles, and checking the computed fonts and colors of headings and text elements in the browser.

**Acceptance Scenarios**:

1. **Given** any page is rendered, **When** inspecting text, **Then** headings use the "Tajawal" font for Arabic and "Montserrat" font for English.
2. **Given** primary buttons, active states, and page sections, **When** checking background or text colors, **Then** they use the new primary colors (Deep Navy `#0A1D3D` and Teal `#0E8F8F`) and secondary grays.

---

### Edge Cases

- **Language Switching (LTL/RTL)**:
  - When switching between Arabic and English, the layout, font family, and logo version must switch correctly (Arabic: "مسار أكاديمي" with Tajawal font, English: "Massar Academy" with Montserrat font).
- **Hardcoded Backend Copy**:
  - Any system-generated emails or PDFs (e.g., student invoice, receipt, parent report) must reference the new brand name.
- **Third-Party Integration Logos**:
  - Icons and titles inside Telegram Bots, WhatsApp notifications, or social widgets must use the updated branding.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display the new logo.svg in the header, footer, landing page, and browser favicon.
- **FR-002**: System MUST replace all user-facing instances of "Nader Gorge" with "Massar Academy" (English) and "مسار أكاديمي" (Arabic).
- **FR-003**: System MUST update CSS/Tailwind configuration to define color tokens for Deep Navy (`#0A1D3D`), Teal (`#0E8F8F`), and Warm Gold (`#D4A017`).
- **FR-004**: System MUST load and configure Google Fonts "Tajawal" (Arabic) and "Montserrat" (English) as the default font stacks.
- **FR-005**: System MUST update all SEO titles, descriptions, and open-graph meta tags across pages.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of user-visible pages display "Massar Academy" / "مسار أكاديمي" instead of "Nader Gorge".
- **SC-002**: Page load time and bundle size must not degrade by more than 2% due to font loading.
- **SC-003**: Responsive design remains fully functional under the new typography (no text overflows or layout shifts).

## Assumptions

- The new logo.svg will be saved under the assets directory and referenced cleanly.
- The fonts will be loaded via standard CDN (Google Fonts) to optimize loading.
- Backend databases or codebase paths do not need physical renaming, only the user-visible string outputs.