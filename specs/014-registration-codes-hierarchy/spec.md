# Feature Specification: Registration, Code System & Content Hierarchy Overhaul

**Feature Branch**: `014-registration-codes-hierarchy`
**Created**: 2026-03-27
**Status**: Draft
**Input**: User description: "Phase 3 — Registration, Code System & Content Hierarchy Overhaul"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Student Registration (Priority: P1)

A new student visits the platform and registers by providing all required personal and academic data in a single registration flow. The form uses conditional logic — selecting an education stage determines available grades, and selecting certain grades reveals track/branch options.

**Why this priority**: Registration is the entry point for every student. Without it, no other feature on the platform can be used. Collecting complete data upfront ensures proper academic tracking and eliminates incomplete profiles.

**Independent Test**: Can be tested by navigating to the registration page, filling out all fields with valid data (including conditional academic fields), submitting, and verifying the student account is created with all data saved correctly.

**Acceptance Scenarios**:

1. **Given** a visitor on the registration page, **When** they fill in all personal fields (full name, phone, student code, DOB, gender, governorate, address, parent phone, parent status) and select education stage "Secondary" and grade "First Secondary", **Then** no track/branch selector is shown and the account is created successfully.
2. **Given** a visitor selecting education stage "Secondary" and grade "Second Secondary", **When** the grade is selected, **Then** a track selector appears with options: Arts, Science.
3. **Given** a visitor selecting education stage "Baccalaureate" and grade "Second Baccalaureate", **When** the grade is selected, **Then** a track selector appears with options: Medicine and Life Sciences, Engineering and Computer Science, Business, Arts and Humanities.
4. **Given** a visitor selecting "First Baccalaureate", **When** the grade is selected, **Then** no track/branch selector is shown.
5. **Given** a visitor submitting the form with a missing required field (e.g., parent phone), **When** they click register, **Then** the form shows a validation error on the missing field and does not submit.
6. **Given** a visitor providing a phone number already registered, **When** they attempt to register, **Then** the system shows an appropriate error message indicating the account already exists.
7. **Given** the parent status section, **When** the student fills it in, **Then** they can independently mark father alive/not and mother alive/not.

---

### User Story 2 - Code System Expansion (Priority: P1)

An admin creates different types of access codes (year, term, month, lesson, video, exam, balance/credit) from the admin panel. Students redeem these codes to unlock specific content. The admin can also create QR codes for distribution and apply discounts.

**Why this priority**: The code system is the primary monetization engine. Expanding code types directly affects revenue capability and content access flexibility. Tied with registration as P1 because codes are useless without registered students and vice versa.

**Independent Test**: Can be tested by an admin creating each of the 7 code types, generating codes, and verifying a student can redeem each type and receive the correct access grant.

**Acceptance Scenarios**:

1. **Given** an admin on the code management page, **When** they create a new code group with type "Year", **Then** the system generates codes that grant access to an entire year of content when redeemed.
2. **Given** an admin creating a "Video" code type, **When** they configure it, **Then** they can select one or more specific videos that the code will unlock.
3. **Given** an admin creating an "Exam" code type, **When** they configure it, **Then** they can select a specific exam that the code will unlock.
4. **Given** an admin creating a "Balance/Credit" code, **When** a student redeems it, **Then** the credit amount is added to the student's account balance for flexible content access.
5. **Given** a generated code group, **When** the admin requests QR codes, **Then** the system generates scannable QR codes for each code in the group.
6. **Given** a student scanning a QR code, **When** the scan completes, **Then** the code is auto-redeemed immediately — the student does NOT need to manually enter the code. Access is granted and the student is taken directly to the unlocked content.
7. **Given** a student with a code string, **When** they enter it manually in the code activation page, **Then** the code is redeemed and access is granted normally.
8. **Given** an existing code, **When** an admin modifies it (extend expiration, change access scope, revoke), **Then** the changes take effect immediately.
9. **Given** a code with a discount, **When** a student redeems it, **Then** the discount is applied correctly to the access purchase.

---

### User Story 3 - Content Hierarchy Restructure (Priority: P2)

An admin restructures the content hierarchy so that a package represents the academic year directly. A package contains terms, terms contain content sections (months), sections contain lessons, and lessons contain videos and other resources. Students who purchase specific content (a lesson, a month, etc.) can navigate directly to that content without going through the full hierarchy.

**Why this priority**: The hierarchy restructure is foundational for the code system to work correctly (term codes, month codes all reference these levels), but can be partially tested with the existing content model first.

**Independent Test**: Can be tested by an admin creating a package, adding multiple terms, adding sections to terms, and verifying the student-facing navigation reflects the full hierarchy with direct-access shortcuts for purchased content.

**Acceptance Scenarios**:

1. **Given** an admin on the content management page, **When** they create a new package, **Then** the package represents the academic year and they can immediately add terms to it.
2. **Given** a package, **When** the admin adds terms, **Then** multiple terms can be created with custom names and ordering.
3. **Given** a term, **When** the admin adds content sections (months), **Then** the sections appear within that term and can be reordered.
4. **Given** the full hierarchy (Package > Term > Section > Lesson), **When** a student views their package, **Then** they see the content organized by term, then by section, then by individual lessons.
5. **Given** a video item in a lesson, **When** the admin configures it, **Then** they can assign a type/tag to the video.
6. **Given** a student who purchased access to a specific month or lesson only, **When** they open the student dashboard, **Then** they see a direct link/shortcut to that content without needing to navigate Package > Term > Section first.

---

### User Story 4 - Admin Student Data Visibility (Priority: P2)

An admin views the student list with all new profile fields (date of birth, gender, parent status, student code, education stage, grade, track) and can filter/search by these fields.

**Why this priority**: Admin visibility into the expanded student data is important for operations but depends on the registration flow being implemented first.

**Independent Test**: Can be tested by verifying the admin student list displays all new columns and filtering by any of the new fields returns correct results.

**Acceptance Scenarios**:

1. **Given** an admin viewing the student list, **When** the page loads, **Then** they see columns for all new fields including student code, DOB, gender, stage, grade, track, and parent status.
2. **Given** the admin student list, **When** the admin filters by education stage "Baccalaureate", **Then** only students in the Baccalaureate stage are shown.
3. **Given** the admin student list, **When** the admin filters by track "Medicine and Life Sciences", **Then** only Second Baccalaureate students with that track are shown.

---

### User Story 5 - Direct Purchase Flow (Priority: P3)

A student can purchase access to content directly through the platform without needing a physical code, using the direct purchase option.

**Why this priority**: Direct purchase is a secondary channel — the primary model is code-based. This enhances convenience but is not critical for launch.

**Independent Test**: Can be tested by a student selecting content to purchase, completing the purchase flow, and verifying access is granted.

**Acceptance Scenarios**:

1. **Given** a student viewing a locked package/term/lesson, **When** they click "Purchase", **Then** they see available purchase options with pricing.
2. **Given** a student with sufficient balance, **When** they confirm the purchase, **Then** access is granted immediately and the balance is deducted.

---

### Edge Cases

- What happens when a student changes their education stage after registration (e.g., transfers from Secondary to Baccalaureate)? The admin MUST be able to update the student's stage, grade, and track.
- How does the system handle a code that was created for a video that is later deleted? The code MUST remain valid but marked as "content unavailable."
- What happens when a student tries to redeem a code for content they already have access to? The system MUST inform the student and not consume the code.
- What happens when an admin revokes a code that was already redeemed? Access granted by the code MUST remain unless the admin explicitly removes the access grant.
- How does the system handle the transition from the old content hierarchy (Package > Section > Lesson) to the new one (Package > Term > Section > Lesson)? Existing content MUST be migrated to the new hierarchy with a default "Term 1" wrapper.
- What happens when a student tries to purchase content but their balance is insufficient? The system MUST block the purchase, show a "recharge your balance" message, and NOT allow negative balance.

## Requirements *(mandatory)*

### Functional Requirements

**Registration:**
- **FR-001**: System MUST collect all 11 personal data fields during registration: full name (four-part), student phone (Dostab), student code (Dostab), date of birth, gender, governorate, address, parent phone, father alive status, mother alive status.
- **FR-002**: System MUST display education stage selection with two options: Secondary (ثانوية) and Baccalaureate (بكالوريا).
- **FR-003**: System MUST display grade options conditionally based on selected stage: First/Second Secondary for Secondary, First/Second Baccalaureate for Baccalaureate.
- **FR-004**: System MUST display track/branch selection only when the student selects Second Secondary (Arts/Science) or Second Baccalaureate (Medicine and Life Sciences, Engineering and Computer Science, Business, Arts and Humanities).
- **FR-005**: System MUST NOT display track/branch selection for First Secondary or First Baccalaureate.
- **FR-006**: System MUST validate all required fields before allowing registration submission.
- **FR-007**: System MUST prevent duplicate registrations with the same phone number.

**Code System:**
- **FR-008**: System MUST support 6 code types: term, month, lesson, video, exam, and balance/credit. (The package itself represents the year, so a "year code" is equivalent to a full package code.)
- **FR-009**: System MUST allow admin to select one or more specific videos when creating a video code type.
- **FR-010**: System MUST allow admin to select a specific exam when creating an exam code type.
- **FR-011**: System MUST add credit to student account balance when a balance/credit code is redeemed.
- **FR-012**: System MUST generate QR codes for any code group upon admin request.
- **FR-013**: System MUST support two code redemption methods: (a) QR scan — auto-redeems instantly without manual entry and redirects to unlocked content, and (b) manual code entry — student types the code string in the activation page. Both methods MUST coexist.
- **FR-014**: System MUST support discount codes that reduce the cost of content access.
- **FR-015**: System MUST allow admin to modify, extend, revoke, or adjust any code at any time.
- **FR-016**: System MUST support direct purchase of content by students without a physical code.
- **FR-017**: System MUST validate code expiration and validity period before allowing redemption.
- **FR-029**: System MUST enforce that student balance is always >= 0. Purchases MUST be blocked if balance is insufficient, with a clear "recharge your balance" message shown to the student.

**Content Hierarchy:**
- **FR-018**: System MUST support the hierarchy: Package (= Year) > Term > Content Section (Month) > Lesson > Video.
- **FR-019**: System MUST allow a package to contain any number of terms.
- **FR-020**: System MUST allow admin to assign a type/tag to each video item.
- **FR-021**: System MUST display the full hierarchy in student-facing navigation.
- **FR-022**: System MUST migrate existing content to the new hierarchy structure with a default "Term 1" wrapper.
- **FR-028**: System MUST provide direct-access navigation — students who purchased a specific lesson or month MUST be able to jump directly to that content from their dashboard without navigating the full hierarchy.

**Admin:**
- **FR-023**: Admin panel MUST display all new student profile fields in the student list.
- **FR-024**: Admin panel MUST support filtering and searching by education stage, grade, track, and other new fields.
- **FR-025**: Admin panel MUST provide interfaces for year/term management in the content structure.
- **FR-026**: Admin panel MUST support QR code generation and printing for code groups.
- **FR-027**: Admin panel MUST provide discount management capabilities.

### Key Entities

- **StudentProfile**: Extended with date of birth, gender, student code (Dostab), governorate, address, parent phone, father alive status, mother alive status, education stage, grade, track/branch.
- **EducationStage**: Secondary or Baccalaureate. Determines available grades.
- **Grade**: First/Second Secondary or First/Second Baccalaureate. Determines available tracks.
- **Track/Branch**: Arts, Science (for Second Secondary), or Medicine and Life Sciences, Engineering and Computer Science, Business, Arts and Humanities (for Second Baccalaureate).
- **Package**: Represents the academic year. Contains terms directly (no separate Year entity).
- **Term**: A container within a package that holds content sections (months).
- **AccessCode**: Extended with new types (package/year, term, month, lesson, video, exam, balance/credit), QR data, expiration, validity period, discount amount.
- **StudentBalance**: Credit balance for flexible content access, adjusted by balance codes and purchases.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Students can complete registration with all required fields in under 3 minutes.
- **SC-002**: 100% of registration submissions contain complete academic data (stage, grade, and track where applicable).
- **SC-003**: All 6 code types can be created by an admin and successfully redeemed by a student within 30 seconds each.
- **SC-004**: QR code scanning results in instant code redemption (under 2 seconds from scan to access grant).
- **SC-005**: Admin can modify any existing code's properties (expiration, scope, revocation) and changes take effect within 5 seconds.
- **SC-006**: Content hierarchy displays correctly at all levels (Package > Term > Section > Lesson) in both admin and student views.
- **SC-007**: Existing content is fully migrated to the new hierarchy with zero data loss.
- **SC-010**: Students with partial access (specific lesson or month) can reach their purchased content in 2 clicks or fewer from the dashboard.
- **SC-008**: Admin student list loads with all new fields and supports filtering with response times under 2 seconds.
- **SC-009**: Conditional registration fields (grade, track) update dynamically without page reload in under 500ms.

## Clarifications

### Session 2026-03-27

- Q: Package-to-Year relationship — is package separate from year? → A: Package IS the year (no separate Year entity). Hierarchy is Package > Term > Month > Lesson. Students who buy specific content (lesson/month) MUST have direct-access shortcuts from the dashboard — no need to navigate the full hierarchy tree.
- Q: Balance behavior when depleted — can it go negative? → A: No. Balance MUST always be >= 0. Purchases blocked if insufficient balance, with "recharge your balance" message.
- Q: QR code security and behavior → A: QR is single-use like regular codes. Scanning auto-redeems instantly (no manual entry needed). Manual code entry also remains available as a parallel method. Both redemption channels coexist.

## Assumptions

- The platform already has a working authentication system (JWT + phone-based) from Phase 1.
- The existing code engine from Phase 1 supports code groups, bulk generation, and single-use codes — this spec extends but does not rebuild it.
- "Dostab" refers to the student database system used by the educational institution; the student phone and code from Dostab are treated as text fields, not validated against an external system.
- Direct purchase (User Story 5) uses the balance/credit system — no external payment gateway integration is required in this phase.
- The QR code generation uses a standard format (e.g., URL-encoded code value) that can be scanned by any QR reader or the platform's built-in scanner.
- Existing packages in the system will be auto-migrated with a default "Year 1" and "Term 1" to maintain backward compatibility.
