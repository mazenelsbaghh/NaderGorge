# Feature Specification: Custom Dynamic Forms System

**Feature Branch**: `067-admin-custom-forms`  
**Created**: 2026-06-01  
**Status**: Draft  
**Input**: User description: "Employment and booking forms: (Independent Forms system that collects data). The Admin views it, and the Admin specifies the fields/data in the form. It could be a booking for the new academic year, or it could be an employment application - these are separate things."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin Custom Form Builder (Priority: P1)

As an Admin, I want to create and configure custom forms (e.g., booking form, recruitment form) by specifying form details and building a list of dynamic fields, so that I can collect custom data for various events or applications.

**Why this priority**: It is the foundation of the feature. Without the form builder, no forms can be defined or rendered.

**Independent Test**: Can be tested by navigating to `/admin/forms/new`, adding a title ("Employment Form"), a slug ("employment"), adding three fields (Name: Text, Email: Email, CV Summary: Long Text), saving it, and verifying it appears in the forms list.

**Acceptance Scenarios**:

1. **Given** the Admin is on the Custom Forms List, **When** they click "Create Form", **Then** they see a builder interface where they can enter Title, Description, and Slug.
2. **Given** the Admin is in the Form Builder, **When** they add a field, **Then** they can choose a type (Text, Long Text, Number, Email, Phone, Select, Checkbox), enter a label, placeholder, and check "Is Required".
3. **Given** the Admin has chosen type "Select", **When** they edit field options, **Then** they can input a list of comma-separated choices for the dropdown.
4. **Given** the Admin saves the form, **When** the slug already exists, **Then** the system displays a validation error because slugs must be unique.

---

### User Story 2 - Public Form Submission (Priority: P1)

As a visitor (student, job applicant), I want to view a custom form via its public URL and submit my data, so that I can apply for a position or book for the new academic year.

**Why this priority**: Core interaction for collecting data. Without it, forms are useless.

**Independent Test**: Can be tested by navigating to `/forms/employment`, filling out the dynamic fields, clicking submit, and seeing a success message.

**Acceptance Scenarios**:

1. **Given** a form with slug `employment` is active, **When** a guest navigates to `/forms/employment`, **Then** the system renders a clean, premium interface containing the form title, description, and the defined fields with proper inputs.
2. **Given** a guest submits the form, **When** any required field is left blank, **Then** the form prevents submission and highlights the missing fields in red.
3. **Given** a guest enters an invalid email or phone number, **When** they submit, **Then** field validation errors are displayed.
4. **Given** all fields are valid, **When** they click "Submit", **Then** the data is saved in the database, a success screen is shown, and the input fields are cleared.

---

### User Story 3 - Admin Submissions Viewer & Moderation (Priority: P2)

As an Admin, I want to view all submissions for each custom form, inspect the submitted data, and update submission status or add internal notes, so that I can process bookings and job applications.

**Why this priority**: Allows admins to review and act on the collected data.

**Independent Test**: Can be tested by opening the submissions page of a form, seeing a table of submissions, clicking on one to open the details modal, updating the status to "Approved", and writing a review note.

**Acceptance Scenarios**:

1. **Given** an Admin is on `/admin/forms`, **When** they click on "Submissions" for a form, **Then** they see a table containing all submissions, sorted by date (newest first).
2. **Given** the submissions table, **When** they click "View" on a row, **Then** a detail panel or modal opens showing all submitted question/field values.
3. **Given** a submission detail page, **When** the Admin changes the status (e.g. from "Pending" to "Reviewed" or "Rejected") and saves, **Then** the submission status updates in the database.
4. **Given** a submission detail page, **When** the Admin writes an internal note and saves, **Then** the note is persisted alongside the submission.

---

### Edge Cases

- **Form Deactivation**: If an Admin deactivates a form (`IsActive = false`), visiting `/forms/[slug]` must return a 404 or a message saying "This form is no longer accepting submissions".
- **Dynamic Field Deletion/Modification**: If an Admin modifies fields on an existing form that already has submissions, existing submissions must still be readable with their original field values (saving submissions as static snapshots is critical).
- **Extremely Long Inputs**: Form fields must support reasonable text length and validate inputs before saving.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Admin MUST be able to define custom forms with a title, description, unique URL slug, and active status.
- **FR-002**: Admin MUST be able to define dynamic fields for a form. Allowed types are: Text, Long Text, Number, Email, Phone, Dropdown (Select), and Checkbox.
- **FR-003**: Dropdown fields MUST allow the Admin to specify comma-separated list of options.
- **FR-004**: System MUST expose public forms at `/forms/[slug]` without requiring authentication.
- **FR-005**: Public form rendering MUST enforce client-side and server-side validation based on field requirements (e.g., required flags, email/phone formatting).
- **FR-006**: Submissions MUST be stored as static JSON data mapping field labels/IDs to values so that subsequent changes to form fields do not corrupt or modify historical submission data.
- **FR-007**: Admin MUST have a submissions dashboard at `/admin/forms/[id]/submissions` listing submissions.
- **FR-008**: Admin MUST be able to view full submission data, update status (`Pending`, `Reviewed`, `Accepted`, `Rejected`), and append internal admin notes.

### Key Entities

- **CustomForm**:
  - `Id` (Guid, PK)
  - `Title` (string)
  - `Description` (string)
  - `Slug` (string, unique index)
  - `IsActive` (bool)
  - `FieldsJson` (string/JSON - stores the list of defined fields: ID, Type, Label, Placeholder, IsRequired, Options)
  - `CreatedAt` / `UpdatedAt` (DateTime)
- **FormSubmission**:
  - `Id` (Guid, PK)
  - `CustomFormId` (Guid, FK to CustomForm)
  - `SubmittedDataJson` (string/JSON - maps field labels or IDs to their submitted values)
  - `Status` (Enum: Pending, Reviewed, Accepted, Rejected)
  - `AdminNotes` (string)
  - `SubmittedAt` (DateTime)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins can define a complete custom form with 5 fields in under 60 seconds.
- **SC-002**: Visitors can submit a form successfully, and the submission is persisted in the database in under 2 seconds.
- **SC-003**: Slugs are strictly validated, preventing any duplicate form URLs.
- **SC-004**: Historical submission data is 100% preserved even if form fields are deleted or modified.

## Assumptions

- Forms are public by default (anyone can submit without logging in).
- Files/attachments are out of scope for v1 of custom forms.
- Re-ordering fields is supported by standard list editing.
