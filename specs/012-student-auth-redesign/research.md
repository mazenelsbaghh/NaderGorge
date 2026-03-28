# Phase 0: Outline & Research

## 1. Adapting Stitch UI to the Dark Mode Design System
**Unknown**: How to map the downloaded Stitch HTML (uses arbitrary Tailwind Hex colors like `bg-[#fcf9ef]`) to the platform's "Editorial Scholar" Dark Mode CSS variables?
**Decision**: We will replace hardcoded hex values from the template with our existing CSS custom properties (e.g., `bg-background`, `bg-surface-container`). We will use a wrapper that applies the "Dark Mode" class or explicitly sets the dark mode CSS variables to mimic the Admin Dashboard.
**Rationale**: Maintains the structural layout of the Stitch design while enforcing the strict color contrast and theming of the admin interfaces per the Constitution.

## 2. Single-Step Registration Backend Support
**Unknown**: Currently, registration creates a generic user, and a secondary step (or code redemption) creates the `StudentProfile`. How do we handle it atomically now?
**Decision**: Update `RegisterStudentCommand.cs` in the backend. Instead of just taking name and phone, it will expand to take `Grade`, `Track`, `ParentPhone`, `School`, `Governorate`, and `City`. The handler will create both the `User` entity and the `StudentProfile` entity in a single transaction.
**Rationale**: Meets the stakeholder's request for single-step registration while ensuring data integrity.
**Alternatives considered**: Doing two API calls from the frontend (one for register, one for profile update). Rejected because atomic transactions on the backend prevent orphaned/incomplete user records if the second call fails.

## 3. Removal of "Code" Prerequisite
**Unknown**: Where was the code required before registration logic?
**Decision**: We will strip out the code validation from the `Register` page entirely. Student registration is fully self-serve. Codes will only be required later for accessing specific content (Packages/Videos).
**Rationale**: Removes onboarding friction as requested.
