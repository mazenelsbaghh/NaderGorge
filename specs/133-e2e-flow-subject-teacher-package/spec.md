# Feature Specification: Comprehensive E2E Test Flow for Course Content Creation and Access

**Feature Branch**: `133-e2e-flow-subject-teacher-package`  
**Created**: 2026-06-16  
**Status**: Draft  
**Input**: User description: "E2E testing of subject creation, teacher creation linked to subject, package creation, terms/months content creation, and free/paid lesson access validation."

## Clarifications

### Session 2026-06-16
- Q: كيف ترغب في معالجة الوصول إلى الحصص المجانية في الاختبارات؟ → A: تتطلب الحصة المجانية إتمام عملية شراء بقيمة 0 ج.م لإنشاء تصريح وصول (StudentAccessGrant).
- Q: أين ترغب في كتابة اختبارات الـ E2E؟ → A: بايثون: إنشاء ملف اختبار pytest جديد باسم tests/test_e2e_content_flow.py.
- Q: ما هو الصف الدراسي المستهدف لهذه الباقة؟ → A: الصف الأول الثانوي (1st Secondary) وتخصص المدرس FirstSecondary.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin Content Management Setup (Priority: P1)

As an Admin, I want to set up a full academic structure (subject, teacher, package, terms, sections, and free/paid lessons with videos, exams, and homework) so that the course content is available for students.

**Why this priority**: It is the foundation of the entire flow. Without this setup, students cannot interact with the content.

**Independent Test**: Can be tested by executing the admin APIs to create each entity and verifying their persistence in the database.

**Acceptance Scenarios**:

1. **Given** an authenticated Admin, **When** they create a Subject, **Then** the Subject is successfully saved.
2. **Given** a Subject, **When** they create a User with role `Teacher` and link them to the Subject via a Teacher Profile, **Then** the Teacher is successfully created and associated.
3. **Given** a Teacher linked to a Subject, **When** they create a Package (Yearly) with target grade matching the Teacher's specialization, **Then** the Package is successfully created.
4. **Given** a Package, **When** they create Terms ("Month 1" and "Month 2") and Content Sections inside them, **Then** they are successfully saved.
5. **Given** a Section, **When** they create a Free Lesson (Price = 0) and a Paid Lesson (Price > 0) inside it, each containing a Video, an inline Exam, and Homework, **Then** all resources are linked correctly.

---

### User Story 2 - Student Free Content Access (Priority: P2)

As a Student, I want to access free lessons (videos, exams, homework) without purchasing the yearly package, while being blocked from paid lessons.

**Why this priority**: Validates that free content is accessible to anyone while paid content remains locked.

**Independent Test**: Can be tested by logging in as a student, acquiring a free lesson grant (0 EGP purchase), and accessing the lesson's resources while asserting that paid lesson detail calls return unauthorized/minimal content.

**Acceptance Scenarios**:

1. **Given** a registered student, **When** they attempt to fetch details of a Paid Lesson, **Then** the API returns minimal details with `hasAccess = false` and no videos/resources.
2. **Given** a registered student, **When** they "purchase" a Free Lesson (Price = 0), **Then** the purchase succeeds with 0 balance deduction and grants access.
3. **Given** a student with a Free Lesson grant, **When** they access the Free Lesson details, **Then** the video details, homework questions, and exams are fully returned.

---

### User Story 3 - Student Paid Content Purchase and Access (Priority: P3)

As a Student, I want to buy the yearly package using my wallet balance to gain full access to all lessons (free and paid) across all terms/months.

**Why this priority**: Validates the purchase flow and cascading access permissions (Package -> Term -> Section -> Lesson).

**Independent Test**: Can be tested by recharging a student's balance, purchasing the yearly package, and verifying that the student now has full access to both free and paid lessons.

**Acceptance Scenarios**:

1. **Given** a student with insufficient balance, **When** they attempt to purchase the yearly package, **Then** the purchase fails.
2. **Given** a student with sufficient balance, **When** they purchase the yearly package, **Then** the price is deducted from their balance, a package-level access grant is created, and full details of both paid and free lessons are accessible.

---

### Edge Cases

- **Teacher Specialization Mismatch**: Attempting to create a Package for a teacher where the package's `TargetGrade` (e.g., "1st Secondary") is not in the teacher's profile `Specialization` must fail.
- **Cascading Access Logic**: A student who purchases a yearly package should not be allowed to buy individual terms or lessons under that package separately (returns a "covered by subscription" error).
- **Zero-Balance Purchase**: Purchasing a free lesson (Price = 0) must succeed even if the student's wallet balance is exactly 0.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Admin Flow**: Admin logs into the admin dashboard (`/api/admin`), creates a subject, a teacher, a yearly package, Month 1 and Month 2 terms, sections, and lessons with videos/exams/homework.
- **Manual QA Student Flow**: Student logs into the student surface (`/api/content`), attempts to view Paid Lesson (blocked), activates Free Lesson (succeeds), views Free Lesson video, purchases Yearly Package using balance, views Paid Lesson video.
- **Docker Acceptance**: Run the integration tests containerized or check that migrations are fully applied.
- **External Dependencies**: None. Uses mock video providers and local database states.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Admin MUST be able to create a Subject with a unique name.
- **FR-002**: Admin MUST be able to create a User with the `Teacher` role and create a corresponding `TeacherProfile` associated with one or more Subject IDs.
- **FR-003**: Admin MUST be able to create a `Package` representing a course/yearly package for a specific Teacher and Subject.
- **FR-004**: Admin MUST be able to create a `Term` inside a Package (representing a monthly course, e.g., "Month 1").
- **FR-005**: Admin MUST be able to create a `ContentSection` inside a Term.
- **FR-006**: Admin MUST be able to create a `Lesson` inside a ContentSection.
- **FR-007**: Admin MUST be able to create a `LessonVideo` and link it to a Lesson.
- **FR-008**: Admin MUST be able to create an inline `Exam` and link it to a Lesson or Video.
- **FR-009**: Admin MUST be able to attach `Homework` to a Lesson.
- **FR-010**: Student MUST be able to "purchase" a lesson with Price = 0 (Free Lesson) for 0 EGP to gain access.
- **FR-011**: Student MUST be able to purchase a Package using their wallet balance to gain cascading access to all child terms, sections, and lessons.
- **FR-012**: The Term Detail Page MUST correctly display the Term price and allow purchasing/enrolling in the Term itself even if its price is 0 EGP (Free), rather than falling back to the Package price/purchase.

### Key Entities

- **Subject**: Represents an academic subject (e.g., Physics).
- **TeacherProfile**: Represents a teacher, linked to a User, has specialization grades and taught subjects.
- **Package**: Represents a yearly course package.
- **Term**: Represents a sub-period inside a package (e.g., "Month 1 Course").
- **ContentSection**: Represents a section inside a term (e.g., "Week 1").
- **Lesson**: Represents a lesson inside a section, can be free (Price = 0) or paid (Price > 0).
- **LessonVideo**: Represents a video linked to a lesson.
- **Exam**: Represents an online exam linked to a lesson.
- **Homework**: Represents homework questions attached to a lesson.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the end-to-end Python pytest suite runs successfully and verifies the creation and access flow without throwing any exception in under 15 seconds.
- **SC-002**: Access validation correctly returns 403 or minimal details for paid lessons before purchase.
- **SC-003**: Access validation correctly returns full details for free lessons after enrolling/purchasing them for 0 EGP.
- **SC-004**: Package purchase successfully unlocks all underlying lessons (Month 1, Month 2, free, and paid).

## Assumptions

- Free lessons require a `StudentAccessGrant` of type `Lesson` to be accessed, which can be acquired via a 0 EGP purchase endpoint call.
- The default timezone Africa/Cairo is used for any time-sensitive queries.
