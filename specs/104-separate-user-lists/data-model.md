# Data Model Mapping: Separate User Lists

We use the existing `AdminUserListDto` contract returned by `adminService.listUsers`.

## Role Classification Rules

The frontend classifies the users into separate tables/lists based on the `roles` array in the `AdminUserListDto` object:

1. **Student (طالب)**:
   - Matches if `roles.includes('Student')` (or `normalizeRole(user) === 'Student'`).
   - Fields displayed: FullName, PhoneNumber, StudentCode, Grade/Track, Status, LastActivity.
   - Specific filters: EducationStage, GradeLevel, StudyTrack, Gender.

2. **Assistant (مساعد)**:
   - Matches if `roles.includes('Assistant')` and does NOT include `Admin`.
   - Fields displayed: FullName, PhoneNumber, Status, LastActivity, Actions.
   - No student-specific filters.

3. **Admin (مدير النظام)**:
   - Matches if `roles.includes('Admin')`.
   - Fields displayed: FullName, PhoneNumber, Status, LastActivity, Actions.
   - No student-specific filters.
