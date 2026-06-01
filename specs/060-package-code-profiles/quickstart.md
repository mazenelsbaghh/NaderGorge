# Quickstart: Package Code Page Profiles

## Goal

Allow admins to customize the code-redemption page per package, while students see either the published package-specific page or the existing fallback experience.

## Backend

1. Add a new `PackageCodePageProfile` entity under `backend/src/NaderGorge.Domain/Entities`.
2. Register the new `DbSet` and EF model configuration in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`.
3. Create an EF migration for the new profile table and indexes.
4. Add admin query/command handlers for:
   - get profile
   - save draft/published profile
   - reset profile to fallback
5. Add a student-facing content query that resolves package data plus published profile/fallback content.
6. Wire the endpoints into `backend/src/NaderGorge.API/Controllers/AdminController.cs` and the relevant content controller.

## Frontend

1. Extend `frontend/src/services/admin-service.ts` with package code-profile admin calls.
2. Extend `frontend/src/services/content-service.ts` with a package code-page read call.
3. Add a new code-profile tab to `frontend/src/app/admin/content/packages/[id]/page.tsx`.
4. Build a package code-profile form using existing shared admin shell/components.
5. Add a package-aware student route under `frontend/src/app/student/code-redemption/`.
6. Reuse `CodeActivationForm` for submission, and swap the surrounding copy/layout from API data.
7. Update locked package CTAs so they deep-link to the package-specific code page instead of only the generic page.

## Verification

1. Create or edit two packages with different code profiles.
2. Publish a custom profile for one package and leave the second on fallback.
3. Open each package code page and confirm the custom/fallback rendering behaves correctly.
4. Reset the custom profile and confirm the package returns to the generic default presentation.
5. Run the relevant frontend Playwright specs and backend tests.

## Expected User Outcome

- Admins manage package-specific code page messaging from the package profile.
- Students land on a package-branded activation page when a package has a published profile.
- Incomplete or reset profiles never break the student experience because fallback remains available.
