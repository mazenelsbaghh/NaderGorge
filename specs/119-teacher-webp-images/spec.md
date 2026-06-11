# Feature Specification: Teacher Image WebP Conversion

**Feature Branch**: `119-teacher-webp-images`  
**Created**: 2026-06-11  
**Status**: Approved  
**Input**: User description: "وعايز صور المدرسين لمي تترفع تتحول بقي ل webp فاهمني"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin uploads or updates Teacher Profile Image (Priority: P1)

As an administrator managing the academy platform, when I add a new teacher or update an existing teacher's profile, any profile image I upload should be saved in WebP format on the server.

**Why this priority**: Profile images are loaded on the package subscription page for students. Storing them in WebP format reduces file size by 90%+, drastically improving the page load speed (TTFB and LCP) for students.

**Independent Test**: Can be tested by uploading a non-WebP profile image for a teacher in the Admin Panel and checking that the resulting URL ends with `.webp` and serves a valid WebP image.

**Acceptance Scenarios**:

1. **Given** the Admin is on the Add/Edit Teacher modal in the Admin Panel, **When** they upload a `teacher_avatar.png` (or `.jpg`), **Then** the image is compressed and converted to WebP in the browser, the file name is updated to end with `.webp`, and it is uploaded to the server successfully.
2. **Given** the image is successfully uploaded, **When** the teacher details are saved, **Then** the database stores the profile image path pointing to a `.webp` file, and the image is rendered correctly using the `.webp` URL.

---

### User Story 2 - Admin uploads Teacher AI Analysis Photo (Priority: P2)

As an administrator, when I upload a teacher's photo for AI analysis, the photo should be saved in WebP format.

**Why this priority**: Helps save storage space on the server and ensures consistency in image asset formats.

**Independent Test**: Can be tested by uploading a JPEG image in the "صورة التحليل للذكاء الاصطناعي" input in the Admin Panel and verifying the uploaded asset is a WebP file.

**Acceptance Scenarios**:

1. **Given** the Admin is on the Add/Edit Teacher modal, **When** they upload a JPEG photo in the AI analysis photo upload area, **Then** the client converts it to WebP format, updates the extension to `.webp`, and uploads it.

---

### User Story 3 - Teacher updates own Profile Image & AI Photo (Priority: P2)

As a Teacher logged into the Teacher Portal, when I upload or update my own profile image or my AI analysis photo, they should be compressed and saved as WebP.

**Why this priority**: Ensures the same optimizations apply when teachers update their profiles directly.

**Independent Test**: Can be tested by logging in as a teacher, uploading a new profile picture, and checking the file format on the server.

**Acceptance Scenarios**:

1. **Given** the Teacher is on their profile settings page, **When** they upload a profile photo, **Then** it is compressed and uploaded as a `.webp` file.
2. **Given** the Teacher uploads an AI photo, **When** it is saved, **Then** it is uploaded as a `.webp` file.

---

### Edge Cases

- **MIME type fallback**: If a browser doesn't support WebP export in `<canvas>` (extremely rare, e.g., old legacy browsers), the frontend will fall back to JPEG or PNG. In this case, the filename extension should fallback to `.jpg` or `.png` accordingly, and the backend must still process it correctly without failing.
- **Corrupt base64 upload**: If the base64 string is corrupted or incomplete, both frontend and backend must catch the error, show a friendly warning toast to the user, and prevent database corruption.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Admin Flow**: Log in as Admin -> Go to Teachers list -> Edit any teacher -> Upload a PNG photo -> Save -> Inspect page and verify the image source URL points to a `.webp` file, and its size is minimal (typically under 100KB).
- **Manual QA Teacher Flow**: Log in as Teacher -> Go to Profile -> Upload profile image -> Save -> Verify image displays correctly and is stored as WebP.
- **Docker Acceptance**: Build docker images and verify they start up cleanly. Verify static folders are accessible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST compress teacher profile images to WebP format client-side before uploading.
- **FR-002**: System MUST compress teacher AI photos to WebP format client-side before uploading.
- **FR-003**: System MUST update the filename extension of the uploaded image to `.webp` when WebP compression is active.
- **FR-004**: Backend upload commands for teacher assets MUST accept the base64 WebP format and save it with the correct file extension on disk, detecting MIME type from the base64 header prefix if present.
- **FR-005**: The database MUST store the reference to the `.webp` image URL.

### Key Entities *(include if feature involves data)*

- **TeacherProfile**: Represents the teacher profile entity. Refers to the `ProfileImageUrl`.
- **TeacherPhoto**: Represents additional teacher photos (like the AI photo). Refers to the `FileUrl`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of successfully uploaded teacher profile images and AI photos on modern browsers MUST be saved in WebP format.
- **SC-002**: Teacher profile image file size on the server MUST be reduced by at least 80% compared to typical raw uploads (average size under 100KB).
- **SC-003**: The admin panel and teacher profile pages MUST load the uploaded images with `.webp` extensions and render them correctly.

## Assumptions

- We assume modern browsers support Canvas-to-WebP export (all mainstream browsers since 2022).
- Backend storage uses local filesystem path `wwwroot/uploads/teacher/` mapped to static serving route.
