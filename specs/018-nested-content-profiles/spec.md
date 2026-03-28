# Feature Specification: Nested Content Profiles

**Feature Branch**: `018-nested-content-profiles`  
**Created**: 2026-03-28  
**Status**: Draft  
**Input**: User description: "خليني اخش جوه الترم واضيف قسم و اخش علي القسم اضيف حصص"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Navigate and Manage Sections within a Term (Priority: P1)

As a content administrator, I want to click on a specific term, enter its dedicated profile page, and manage its learning sections (أقسام), so that I can organize the educational term into logical chapters or units.

**Why this priority**: Sections are the immediate children of Terms. Without the ability to create and manage sections inside a term, the educational structure breaks down.

**Independent Test**: Can be fully tested by creating a dummy Term, clicking on it, verifying the Term profile page loads, and then adding a new Section with a title and order.

**Acceptance Scenarios**:

1. **Given** the administrator is on a Package Profile page viewing a list of Terms, **When** they click on a specific Term, **Then** they are navigated to the Term Profile page showing the Term's details.
2. **Given** the administrator is on the Term Profile page, **When** they fill out the Add Section form and submit, **Then** the new section appears immediately in the sections list.

---

### User Story 2 - Navigate and Manage Lessons within a Section (Priority: P1)

As a content administrator, I want to click on a specific section, enter its dedicated profile page, and manage its lessons (حصص), so that I can populate the section with actual educational content and videos.

**Why this priority**: Lessons are the core content delivery mechanism. They must be added inside sections for students to consume them in sequence.

**Independent Test**: Can be fully tested by clicking on an existing Section, verifying the Section profile page loads, and adding a new Lesson with title, summary, and order.

**Acceptance Scenarios**:

1. **Given** the administrator is on a Term Profile page viewing a list of Sections, **When** they click on a specific Section, **Then** they are navigated to the Section Profile page showing the Section's details.
2. **Given** the administrator is on the Section Profile page, **When** they fill out the Add Lesson form (title, summary, order) and submit, **Then** the new lesson appears immediately in the lessons list.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a Term Profile view that displays the Term's basic information and lists all its associated Sections.
- **FR-002**: System MUST allow administrators to create a new Section within a Term by providing a Title and an Order number.
- **FR-003**: System MUST provide a Section Profile view that displays the Section's basic information and lists all its associated Lessons.
- **FR-004**: System MUST allow administrators to create a new Lesson within a Section by providing a Title, Summary, and Order number.
- **FR-005**: System MUST enforce correct sequential ordering (Order field) for sections within a term and lessons within a section.

### Key Entities

- **Term**: An educational semester/term that acts as a container for Sections.
- **ContentSection**: A chapter or unit within a Term that acts as a container for Lessons.
- **Lesson**: A specific class/lecture within a Section that will later hold videos, resources, and homework.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Administrators can successfully navigate from Package -> Term -> Section -> Lesson in a seamless flow.
- **SC-002**: A new section can be created inside a term and appears in the list without requiring a full page reload.
- **SC-003**: A new lesson can be created inside a section and appears in the list without requiring a full page reload.
- **SC-004**: Navigation breadcrumbs consistently inform the administrator of their current depth in the hierarchy.
