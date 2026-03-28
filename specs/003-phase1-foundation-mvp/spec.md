# Feature Specification: Phase 1 — Foundation and MVP Launch

## 1. Feature Description
Phase 1 aims to launch the foundational MVP of the Nader George Educational Platform. This phase focuses on core infrastructure, allowing students to register via a two-step process, authenticate with phone numbers, activate access codes, and consume structured educational content (packages, sections, lessons, videos, and basic MCQ exams). It also includes the initial versions of the public website, student dashboard, and admin panel for content and code management.

## Clarifications
### Session 2026-03-22
- Q: Should Phase 1 MVP actively enforce a maximum number of devices per student account? → A: Target Strict Enforcement: Enforce maximum devices per student account. Admins must have the ability to manually remove a registered device.
- Q: If a student scores below the pass threshold on a lesson's MCQ exam, how does the system handle progression? → A: Teacher/Assistant-Controlled Gating: Passing score is required to proceed. Failing blocks progression until retake, or manual unlock by Teacher/Admin/Assistant.
- Q: What is the exact enforcement when a student hits the maximum allowed watch duration for a video? → A: Hard Lock with Manual Override: Playback is locked upon reaching the limit, requiring manual intervention (limit increase or unlock) by an admin or assistant to continue watching.

## 2. User Scenarios & Testing

### Scenario 1: Student Onboarding and Code Activation
- **User:** New Student
- **Action:** Lands on the public site, clicks Register, completes Step 1 (Name, Phone, Grade, Track). Logs in, attempts to activate a Code. Modal prompts for Step 2 (Parent Phone, Governorate, City, School). Completes Step 2 and activates the Code.
- **Expected Outcome:** Account is created, secure session established, Code is validated and consumed, and the student is granted access to the associated Package or Lesson.

### Scenario 2: Content Consumption
- **User:** Enrolled Student
- **Action:** Navigates to the Student Dashboard, clicks on an unlocked Package, drills down to a Lesson, and starts a Video.
- **Expected Outcome:** Breadcrumb navigation updates. Video player loads via the `VideoProviderAbstraction`. Video watch events (started, current duration, completed) are sent to the backend.

### Scenario 3: Basic Exam Completion
- **User:** Enrolled Student
- **Action:** Opens an MCQ exam attached to a Lesson, submits answers.
- **Expected Outcome:** The system auto-grades the exam instantly, displays the score, checks it against the pass threshold, and records the attempt.

### Scenario 4: Admin Content and Code Management
- **User:** Admin
- **Action:** Logs into the Admin panel, creates a new Package and Lesson hierarchy, then generates a batch of 100 single-use access codes for that package.
- **Expected Outcome:** Content is saved to the database. 100 unique, alphanumeric codes are generated, hashed/stored, and available for export.

## 3. Functional Requirements

### 3.1 Authentication & Registration
- **FR_1.1.1:** The system shall support phone-number-based registration and secure login using standard modern authentication protocols. The system must enforce a strict maximum number of registered devices per student account; admins shall have the ability to manually remove registered devices to allow logins from new ones.
- **FR_1.1.2:** The system shall implement a two-step registration flow: Step 1 (Low friction) and Step 2 (Operational block triggered upon first code activation or content access).
- **FR_1.1.3:** The system shall implement four core roles: Admin, Teacher, Assistant, and Student.

### 3.2 Public Website & Dashboards
- **FR_1.2.1:** The system shall expose a public site with a landing page, package overview, FAQ, and auth entry points.
- **FR_1.2.2:** The Student Dashboard shall display available packages, recent lessons, upcoming exams, progress, and provide quick access to "resume study".
- **FR_1.2.3:** The Admin Panel shall allow CRUD operations for Users (Students/Teachers/Assistants), Content (Packages/Sections/Lessons), Code Groups, and the basic Question Bank.

### 3.3 Content & Examination
- **FR_1.3.1:** The system shall support a hierarchical content model: Program > Package > Content Section > Lesson.
- **FR_1.3.2:** Lesson pages shall support embedded videos (locked to allowed speeds, tracking watch limits), text summaries, and downloadable resources. When a student reaches the maximum allowed watch limit for a video, playback shall be hard-locked; an admin or assistant must manually increase the limit or unlock the video to allow further playback.
- **FR_1.3.3:** The system shall support instant-graded MCQ exams with pass thresholds set by Teachers or Assistants. If a student fails to meet the passing score, progression to the next lesson is strictly blocked. The student must either retake and pass the exam, or wait for a Teacher, Admin, or Assistant to manually unlock the next lesson.

### 3.4 Code Engine & Audit
- **FR_1.4.1:** The Admin panel shall support bulk generation of single-use, alphanumeric access codes assigned to specific Packages or Lessons.
- **FR_1.4.2:** Code activation shall immediately grant the student access and mark the code as consumed, logging the activation timestamp and user.
- **FR_1.4.3:** The system shall maintain audit logs for code generation, redemption, content edits, and role changes.

## 4. Success Criteria
- **SC_01:** 100% of new users can complete the two-step registration and code redemption flow without errors or skipped required fields.
- **SC_02:** Video watch endpoints sustain concurrent pings from 1,000 simulated students without dropping logs or exceeding 500ms response times.
- **SC_03:** An Admin can generate 1,000 access codes and export them in under 5 seconds.
- **SC_04:** MCQ exams calculate and return final scores within 1 second of submission.
- **SC_05:** Complete UI/UX implementation of the Public Website, Student Dashboard, and Admin Panel exists and passes basic usability testing across desktop and mobile.

## 5. Key Entities (Data Draft)
- **User**, **StudentProfile**, **UserSession**
- **Package**, **ContentSection**, **Lesson**, **LessonVideo**
- **CodeGroup**, **AccessCode**, **StudentAccessGrant**
- **Exam**, **ExamQuestion**, **StudentExamAttempt**
- **AuditLog**, **VideoWatchEvent**

## 6. Assumptions & Scope Boundaries
- **Assumptions:** We are using YouTube initially for video hosting, hidden behind a provider abstraction. SMS/WhatsApp integrations are mocked or use a very basic provider for Phase 1.
- **Out of Scope for Phase 1:** Homework (essays), Parent portal, complex AI features, advanced tracking (skip prevention algorithms), and complex Assistant role boundaries (all Assistants get basic read access for now).
