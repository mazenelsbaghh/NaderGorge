# Feature Specification: Student & Teacher AI-Generated Avatars

**Feature Branch**: `065-student-teacher-avatars`  
**Created**: 2026-06-01  
**Status**: Draft  
**Input**: User description: "الطالب يختار أفاتار في التسجيل — استخدم Gemini لإنشاء صور كاريكاتيرية لشخصيات تاريخية وعلماء — تظهر في أي مكان فيه صورة بروفايل — للأكاونتات وللمدرس"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Student Selects an Avatar During Registration (Priority: P1)

A new student registers on the platform. During the registration wizard, they encounter an avatar selection step. They see a gallery of pre-generated AI caricature avatars featuring historical figures and scientists (e.g., Einstein, Ibn Sina, Marie Curie, Newton, Al-Khwarizmi). The student taps on their preferred avatar, and it becomes their profile picture throughout the platform.

**Why this priority**: This is the core of the feature — the registration flow is the most natural place to introduce avatar selection, and it directly enhances the student's first impression of the platform.

**Independent Test**: Can be fully tested by registering a new student account and verifying the selected avatar appears on the dashboard, community feed, and navigation.

**Acceptance Scenarios**:

1. **Given** a student is at Step 1 (Personal Data) of registration, **When** they view the form, **Then** they see a horizontally scrollable avatar gallery with at least 12 pre-generated caricatures with names underneath each one.
2. **Given** the avatar gallery is visible, **When** the student taps on an avatar, **Then** it gets a highlighted selection indicator (accent border + check badge) and the chosen avatar ID is stored in form state.
3. **Given** no avatar has been selected, **When** the student proceeds, **Then** a default avatar (e.g., a graduation cap icon or the first-letter circle) is auto-assigned.
4. **Given** registration completes successfully, **When** the student lands on their dashboard, **Then** their chosen avatar is displayed in the hero greeting area and the navigation.

---

### User Story 2 - Avatar Appears Everywhere a Profile Image Is Expected (Priority: P1)

Once a student has an avatar, it displays consistently across all platform touchpoints: the student dashboard hero, navigation bar (desktop sidebar & mobile drawer), community feed posts, community comments, lesson comments, and the admin's student profile view.

**Why this priority**: Consistency is key — an avatar that shows in registration but nowhere else feels broken.

**Independent Test**: After selecting an avatar, navigate to every screen where the student's identity is shown and verify the avatar appears.

**Acceptance Scenarios**:

1. **Given** a logged-in student with a selected avatar, **When** they view their dashboard, **Then** the avatar appears in the StudentHero greeting section.
2. **Given** a logged-in student with a selected avatar, **When** they view the desktop sidebar, **Then** the avatar appears next to the graduation cap icon at the top.
3. **Given** a student posts in the community feed, **When** other students view the post, **Then** the post author's avatar appears instead of the first-letter circle.
4. **Given** a student has not selected an avatar, **When** their profile is displayed anywhere, **Then** a graceful fallback is shown (first letter of name in a colored circle).

---

### User Story 3 - Admin/Teacher Avatar Setup (Priority: P2)

The admin or teacher can have an avatar too. For the teacher, the existing teacher photo system (used for AI mindmap generation) also serves as the display avatar. If no photo is uploaded, a teacher-specific default avatar (e.g., a scholarly figure) is shown.

**Why this priority**: Teachers are secondary users — students see teacher avatars in lesson content and comments, but teachers don't interact with the avatar selection as frequently.

**Independent Test**: Upload a teacher photo via admin panel and verify it appears as the teacher's avatar in lesson comments and the admin's profile view.

**Acceptance Scenarios**:

1. **Given** a teacher has an uploaded photo, **When** their identity is shown in lesson comments or content, **Then** their photo is used as their avatar.
2. **Given** a teacher has no uploaded photo, **When** their identity is shown, **Then** a default teacher avatar icon is displayed.

---

### User Story 4 - Student Changes Avatar After Registration (Priority: P3)

A student who already has an account wants to change their avatar. They navigate to their settings or the theme settings panel and find an avatar gallery where they can select a different caricature.

**Why this priority**: This is a nice-to-have — students can always re-register or can be served on next iteration.

**Independent Test**: Log in as an existing student, navigate to settings, change avatar, and verify the new avatar appears everywhere.

**Acceptance Scenarios**:

1. **Given** a logged-in student opens the mobile drawer or theme settings, **When** they look for avatar options, **Then** they see the same avatar gallery from registration.
2. **Given** the student selects a new avatar, **When** they confirm, **Then** the avatar updates everywhere without page reload.

---

### Edge Cases

- What happens when an avatar image file is missing from the server? → Fallback to first-letter circle.
- What happens when a student registers offline or with poor connectivity? → Avatar selection is optional; default is assigned if selection fails.
- What if the pre-generated avatar gallery is empty (images haven't been seeded)? → Show a set of generic colored gradient circles with initials until avatars are seeded.
- What if two students choose the same avatar? → Allowed — avatars are not unique identifiers.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a gallery of at least 12 pre-generated AI caricature avatars depicting famous historical figures and scientists.
- **FR-002**: System MUST allow students to select one avatar during registration (Step 1: Personal Data).
- **FR-003**: System MUST store the selected avatar reference (avatar ID/slug) on the student's profile record.
- **FR-004**: System MUST display the student's selected avatar in all profile-displaying UI locations: dashboard hero, desktop sidebar, mobile drawer, community posts, community comments, lesson comments.
- **FR-005**: System MUST provide a graceful fallback (first-letter colored circle) when no avatar is selected or the avatar image is unavailable.
- **FR-006**: System MUST allow students to change their avatar from the theme settings panel.
- **FR-007**: System MUST use the existing teacher photo as the teacher's avatar, with a fallback default.
- **FR-008**: System MUST include the student's avatar URL in the authentication token payload or user profile API response so the frontend can display it without an extra API call.
- **FR-009**: System MUST pre-generate avatar images using AI (Gemini image generation) and store them as static assets served from the backend.
- **FR-010**: System MUST create a reusable avatar component that handles all display sizes and fallback logic.

### Key Entities

- **Avatar Catalog**: A static set of pre-generated images identified by a slug (e.g., `einstein`, `ibn-sina`, `curie`). Each has a display name, image URL, and category (scientist, historian, mathematician, etc.).
- **User Avatar Reference**: A new field on the StudentProfile entity pointing to the chosen avatar slug. No separate DB table needed — just a string column.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of new student registrations are presented with the avatar selection gallery.
- **SC-002**: Students can complete avatar selection in under 5 seconds (single tap/click).
- **SC-003**: Avatars display correctly across all 6 profile-showing UI locations without any broken image fallbacks.
- **SC-004**: Avatar images load in under 500ms on a standard mobile connection (images are pre-generated and optimized).
- **SC-005**: Students can change their avatar and see the change reflected immediately across all locations without full page reload.
- **SC-006**: 95% of students who reach the registration form select an avatar (high engagement via attractive gallery design).

## Assumptions

- The AI caricature images will be pre-generated at development/deploy time (not generated on-the-fly per student request). This avoids API costs and latency.
- Avatar images will be stored as static PNG/WebP files in the backend's `wwwroot/uploads/avatars/` directory, following the existing static file serving pattern.
- The avatar gallery will contain 12-20 diverse characters representing various cultures, genders, and fields of knowledge (sciences, arts, history, mathematics).
- The existing `TeacherPhoto` entity and upload flow will not be modified — it will simply be reused for teacher avatars.
- Mobile-first design: the avatar gallery must be fully usable on small screens via horizontal scrolling.
- Avatar selection is optional — students are never blocked from completing registration without choosing one.
