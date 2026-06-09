# Quickstart: Teacher Binding and Completion

This guide helps you set up and test the multi-teacher binding and dashboard features.

## Seeding Test Data

For local testing, you can use the E2E testing controller to register a teacher, link them to subjects, and create packages.

1. **Register a Teacher**:
   Use `POST /api/e2e/reset` or standard admin user creation to seed a teacher user with the "Teacher" role.
2. **Assign Subject**:
   Ensure the teacher is assigned a subject via the `TeacherSubject` table.
3. **Verify Dashboard**:
   - Access the teacher portal on `http://localhost:8741/` or `teacher.massar-academy.net`.
   - The statistics should reflect real data from the seeded course.

## Verification Checklist

1. **Dashboard Verification**: Log in as a teacher and verify the stats values match the database.
2. **No Fallbacks Verification**: Try to trigger `CreateInlineExamCommand` or `BulkGenerateCodesCommand` with no resolved teacher and verify it returns a 400 Bad Request instead of defaulting.
3. **Cross-Teacher Verification**: Try to update Teacher B's packages as Teacher A and confirm the API returns a 403 Forbidden.
4. **Student Portal Branding**: Activate a package as a student and confirm the teacher's profile details are visible in the package view.
