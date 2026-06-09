# Research: Call Center CRM Architecture

## Decision 1: CRM Database Schema Design

### Chosen Approach
We will introduce two new database entities:
1. `CrmStudentStatus`: A 1-to-1 relationship table linked to `User` (specifically users with the `Student` role). This holds the current CRM state of the student, including status (`InProgress`, `Cold`, `Closed`, etc.), `AssignedAgentId`, priority, and next follow-up date.
2. `CrmCallLog`: A history table recording all call interactions made by staff to the student. Each log is linked to the student and the logging staff member (`AgentId`).

### Rationale
- Splitting the CRM tracking details into a 1-to-1 extension table (`CrmStudentStatus`) keeps the core `User` / `StudentProfile` tables clean and decoupled from call center operations.
- The `CrmCallLog` provides a history, allowing supervisors to audit performance and agents to recall previous conversations.

### Alternatives Considered
- *Option A: Inline fields on StudentProfile*: Rejected because CRM operations are transient, staff-facing metadata. Keeping them inline would pollute student domain models and cause lock contention during high-volume call logging.

---

## Decision 2: Access Control and Row-Level Filtering

### Chosen Approach
- Access to CRM endpoints is protected by the `crm.manage` permission claim at the controller level.
- Row-level filtering is applied in the MediatR query handlers:
  - If the authenticated user is an `Admin` or `Supervisor` role type, they see all student CRM data.
  - If the user is an `Assistant` or `Staff` (call center agent), the query handler filters results using `WHERE AssignedAgentId = CurrentUserId`.

### Rationale
- This satisfies the strict specification rule: *"منع Agent من رؤية قوائم غير مسندة إليه إلا بصلاحية"* (Prevent agent from seeing unassigned lists unless privileged).
- Performing this check in the query handlers ensures secure data partitioning at the database retrieval level.

---

## Decision 3: WhatsApp Action Normalization

### Chosen Approach
- A quick action button will generate standard `wa.me` links pointing to `https://wa.me/20[NormalizedPhoneNumber]?text=[TemplateText]`.
- Phone numbers will be normalized programmatically (removing spaces, leading zeroes, dashes, and prepending Egypt's country code `20`).

### Rationale
- Native browser-based WhatsApp redirection is fully client-side, extremely robust, requires no API credentials, and works out of the box.
