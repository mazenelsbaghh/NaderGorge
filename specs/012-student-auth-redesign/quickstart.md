# Quickstart: Student Auth Redesign

## Setup

1. Check out the feature branch: `git checkout 012-student-auth-redesign`
2. Ensure you have the `login_design.html` reference downloaded which contains the exact glassmorphism classes and structural layout from the Stitch prototype.

## Implementation Steps (Overview)

### 1. Backend Modifications
- Open `/backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterStudentCommand.cs`.
- Update the properties to include `ParentPhone`, `GradeId`, `TrackId`, `School`, `Governorate`, `City`.
- Modify the command handler to create both the `User` with roles AND the `StudentProfile` entity atomically in a single EF Core unit of work.
- Update `/backend/src/NaderGorge.API/Controllers/AuthController.cs` if the route payload signature needs adjustment.

### 2. Frontend Redesign (Login)
- Backup and rewrite `/frontend/src/app/login/page.tsx` entirely based on the new Stitch HTML.
- Replace the hex colors with existing CSS Custom Properties from `tailwind.config.ts`.
- Ensure Next.js client-side interactivity works.

### 3. Frontend Redesign (Registration)
- Convert `/frontend/src/app/register/page.tsx` into a single form.
- Apply the same dark mode UI wrapper used in `login/page.tsx`.
- Map all the inputs (Name, Phone, Password, Grade, Parent Phone).
- Refactor existing Next.js logic to no longer ask for an access code before registration.
