# Feature Specification: fix-forms-id-mismatch

**Feature Branch**: `087-fix-forms-id-mismatch`  
**Created**: 2026-06-07  
**Status**: Draft  
**Input**: User description: "Fix the 400 Bad Request error on custom forms PUT (ID mismatch) and submission status PUT."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin can update custom form details (Priority: P1)

Administrators need to be able to edit custom form metadata (title, slug, starts/expires dates, fields configuration) and toggle the active state of forms from the admin interface. Currently, any attempt to toggle or edit custom forms results in a 400 Bad Request.

**Why this priority**: High priority as it is currently impossible to manage forms once created.

**Independent Test**:
- Create a new custom form or choose an existing one.
- Go to the admin forms list and toggle the "حالة الاستقبال" (Active State) or click "تعديل" (Edit), change some fields, and save.
- Verify that the form updates successfully and no 400 Bad Request error is returned.

**Acceptance Scenarios**:

1. **Given** an existing custom form, **When** the administrator toggles its active/inactive status from the admin forms list, **Then** the request completes successfully with a 200 OK response, and the new state is saved.
2. **Given** the custom form edit page, **When** the administrator modifies the form's title or fields and clicks "حفظ ونشر النموذج", **Then** the request completes successfully, and the changes are persisted.

---

### User Story 2 - Admin can update submission status (Priority: P2)

Administrators need to update the status (Pending, Reviewed, Accepted, Rejected) and admin notes of submitted forms.

**Why this priority**: Essential workflow for managing submissions, which would otherwise fail with the same ID mismatch pattern.

**Independent Test**:
- Go to a form's submissions list.
- Click to update a submission's status or add notes.
- Verify that the submission status updates successfully.

**Acceptance Scenarios**:

1. **Given** a form submission, **When** the administrator updates its status or adds review notes, **Then** the request completes successfully with 200 OK, and the status changes.

---

### Edge Cases

- **Path vs. Body ID Mismatch**:
  - What happens if the ID in the route path differs from the ID in the body?
  - System handles this by returning a 400 Bad Request with a clear message (e.g., "Form ID mismatch" or "Submission ID mismatch") to prevent security bypasses or incorrect entity updates.
- **Empty or Malformed GUIDs**:
  - What happens if a malformed ID is passed?
  - The framework (ASP.NET Core) will automatically block the request or return a 400 Bad Request due to model binding failure.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The admin interface MUST send the form `id` inside the PUT request body payload when calling `/api/admin/forms/{id}` to update form details.
- **FR-002**: The admin interface MUST send the `submissionId` inside the PUT request body payload when calling `/api/admin/forms/submissions/{submissionId}/status` to update submission status.
- **FR-003**: The backend API MUST validate that the URL path ID matches the payload ID, and process the update if they match.

### Key Entities

- **CustomForm**: Represents a custom form created by the administrator. Attributes include `Id` (GUID), `Title`, `Description`, `Slug`, `IsActive`, `CoverImageUrl`, `StartsAt`, `ExpiresAt`, and `FieldsJson`.
- **FormSubmission**: Represents a response submitted by a user for a specific custom form. Attributes include `Id` (GUID), `CustomFormId` (GUID), `SubmittedDataJson`, `Status`, and `AdminNotes`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of valid form update requests from the admin dashboard must succeed with a 200 OK response.
- **SC-002**: 100% of valid submission status update requests must succeed with a 200 OK response.

## Assumptions

- The backend APIs themselves (`PUT /api/admin/forms/{id}` and `PUT /api/admin/forms/submissions/{submissionId}/status`) are correctly implemented and do not require logic changes, as they already perform validation checks.
- The issue is entirely on the client-side/frontend service layer where these IDs are omitted from the JSON request payloads.
