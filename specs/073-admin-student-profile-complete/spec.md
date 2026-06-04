# Feature Specification: Admin Student Profile — Complete Data Display

**Feature Branch**: `073-admin-student-profile-complete`
**Created**: 2026-06-04
**Status**: Draft
**Input**: User description: "Display ALL student profile data in the admin student detail page. Currently only 6 fields are shown out of 25+ stored in the database."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Complete Student Personal Data (Priority: P1) 🎯 MVP

As an admin, I want to see all personal data for a student when I open their profile page, so I can get a complete picture without having to check the database directly.

**Why this priority**: This is the core gap — admins currently only see 6 out of 25+ fields. The most critical fields (date of birth, gender, governorate, address, parent status) are all missing from the detail page.

**Independent Test**: Navigate to `/admin/users/{studentId}` and verify all personal fields appear: full name, phone, date of birth, gender, nationality, governorate, district, address, secondary phone, student code.

**Acceptance Scenarios**:

1. **Given** an admin navigates to a student's profile page, **When** the page loads, **Then** all personal data fields (name, phone, DOB, gender, nationality, governorate, district, full address, secondary phone, student code) are visible in the "نظرة عامة" tab.
2. **Given** a student has empty/null optional fields (e.g., nationality, secondary phone), **When** the admin views the profile, **Then** those fields show "غير متوفر" as a graceful fallback.
3. **Given** a student's date of birth is `2019-12-17`, **When** the admin views the profile, **Then** the date appears formatted as `17/12/2019` (DD/MM/YYYY).

---

### User Story 2 - View Parent & Family Information (Priority: P1)

As an admin, I want to see the full family/parent data for a student including parent phones, alive/deceased status, and parent dates of birth.

**Why this priority**: Parent information is critical for contacting guardians and understanding the student's family situation (e.g., orphan status affects financial aid decisions).

**Independent Test**: Navigate to a student profile and verify the parent section shows father phone, mother phone, secondary parent phone, alive/deceased status for both parents, and parent dates of birth.

**Acceptance Scenarios**:

1. **Given** a student whose father is deceased and mother is deceased, **When** the admin views the profile, **Then** the parent section shows "الأب: متوفى" in red and "الأم: متوفاة" in red.
2. **Given** a student with a mother phone `01012345678`, **When** the admin views the profile, **Then** the mother phone is displayed alongside the father phone.
3. **Given** a student with no secondary parent phone, **When** the admin views, **Then** the secondary parent phone field shows "غير متوفر".

---

### User Story 3 - View School & Academic Information (Priority: P2)

As an admin, I want to see the student's school name, school type, education stage, grade level, and study track in a dedicated academic section.

**Why this priority**: Academic data is needed for curriculum assignment and grouping students by their educational level and track.

**Independent Test**: Navigate to a student profile and verify the academic section shows school name, school type (e.g., "حكومية"/"لغات"), education stage, grade level, and study track.

**Acceptance Scenarios**:

1. **Given** a student in "SecondBaccalaureate" with track "EngineeringAndComputerScience", **When** the admin views the profile, **Then** the academic section shows "ثانية بكالوريا" and "الهندسة وعلوم الحاسب".
2. **Given** a student with school type "Language", **When** the admin views the profile, **Then** the school type displays as "لغات".

---

### Edge Cases

- What happens when a student has no `StudentProfile` record (old account)? Display "لا توجد بيانات بروفايل" message.
- What happens when the profile is incomplete (`IsProfileComplete = false`)? Show a warning badge.
- What if the student has no study track (first-year students)? The track field should be hidden or show "لا ينطبق".
- What if parent dates of birth are null? Show "غير متوفر".

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Backend DTO MUST include all fields from `StudentProfile` entity: DateOfBirth, Gender, Nationality, Governorate, District, Address, SecondaryPhone, ParentPhone, SecondaryParentPhone, MotherPhone, IsFatherAlive, IsMotherAlive, FatherDateOfBirth, MotherDateOfBirth, SchoolName, SchoolType, EducationStage, GradeLevel, StudyTrack, StudentCode, IsProfileComplete.
- **FR-002**: Backend query handler MUST populate all new DTO fields from the loaded `StudentProfile` entity.
- **FR-003**: Frontend TypeScript interface MUST match the updated backend DTO 1:1.
- **FR-004**: Admin student profile page MUST display all fields organized in clearly labeled sections: Personal Data, Parent/Family Data, School & Academic Data, Address & Contact.
- **FR-005**: System MUST display enum values in Arabic-friendly labels (e.g., `SchoolType.Language` → "لغات", `Gender.Male` → "ذكر").
- **FR-006**: System MUST format all dates in DD/MM/YYYY format for Arabic locale consistency.
- **FR-007**: System MUST show "غير متوفر" for any null/empty optional fields.
- **FR-008**: Remove the non-functional "البريد الإلكتروني" field since no email is stored in the system.

### Key Entities

- **User**: Core user record — FullName, PhoneNumber, IsActive, IsProfileComplete, CreatedAt.
- **StudentProfile**: Extended student data — personal details, parent info, school info, academic info, theme preferences.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin can see 100% of stored student data fields (25+ fields) on the student profile page without checking the database.
- **SC-002**: Page loads all profile data within 2 seconds for any student.
- **SC-003**: All enum fields display Arabic-friendly labels instead of raw enum strings.
- **SC-004**: Zero "undefined" or raw JSON values visible in the UI for any student profile.

## Assumptions

- The existing admin authentication and authorization system is reused — no new permissions needed.
- The `StudentProfile` entity schema is stable and no new database migrations are needed (we're reading existing columns).
- The admin panel uses the existing "Editorial Scholar" theme tokens and design system.
- RTL Arabic layout is already implemented in the admin shell.
- Mobile responsiveness for the admin panel is secondary — desktop-first is acceptable.
