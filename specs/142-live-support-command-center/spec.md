# Feature Specification: Live Support Command Center

**Feature Branch**: `[142-live-support-command-center]`  
**Created**: 2026-06-21  
**Status**: Draft  
**Input**: User-approved feature brief for a live support channel serving authenticated students and outside visitors, with automatic workload-aware staff assignment, a waiting queue, complete student context and actions inside the staff conversation workspace, and full administrative oversight.

## Clarifications

### Session 2026-06-21

- Q: متى يصبح الموظف مؤهلًا لاستقبال محادثات جديدة؟ → A: يبدأ تلقائيًا عند تسجيل الحضور ويستمر حتى تسجيل الانصراف، دون إيقاف استقبال يدوي أثناء فترة الحضور.
- Q: متى تُعاد محادثات الموظف بعد انقطاع اتصاله؟ → A: بعد انقطاع متواصل لمدة دقيقتين تُعاد محادثاته للتوزيع؛ إذا عاد قبل اكتمال المهلة يحتفظ بملكيتها.
- Q: ماذا يحدث إذا أراد الطالب التواصل بعد إغلاق المحادثة؟ → A: المحادثة المغلقة تصبح للقراءة فقط، ويبدأ الطالب محادثة جديدة من زر مخصص؛ بعد الإغلاق يظهر تقييم بالنجوم يؤثر في قياس أداء الموظف.
- Q: على مَن يُحسب التقييم إذا شارك أكثر من موظف؟ → A: نفس التقييم يُحسب بالتساوي على كل موظف امتلك المحادثة خلال دورة معالجتها.
- Q: ماذا يحدث عندما لا يوجد أي موظف مسجل حضور؟ → A: يُمنع بدء محادثة جديدة ويظهر أقرب موعد متاح للدعم؛ المحادثات الموجودة بالفعل تظل محفوظة ولا تُحذف.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start and continue a support conversation (Priority: P1)

An authenticated student starts live support without re-entering identity details, while an outside visitor starts as a guest by entering a name and phone number. Both can exchange messages in real time, reconnect without losing the conversation, and understand whether they are waiting, connected to a staff member, or finished.

**Why this priority**: Without a reliable customer entry flow and persistent conversation, no other support capability has value.

**Independent Test**: Start one conversation as a signed-in student and another as a guest, exchange messages, disconnect and reconnect, and verify identity treatment, message history, and status continuity.

**Acceptance Scenarios**:

1. **Given** a signed-in student, **When** the student opens live support, **Then** the conversation is associated with that student automatically without requesting a name or phone number again.
2. **Given** a visitor without an account session, **When** the visitor supplies a name and valid phone number, **Then** a guest conversation starts without exposing or automatically linking any matching student account.
3. **Given** a participant has an open or waiting conversation, **When** the participant reconnects from the same recognized session, **Then** the existing conversation and messages are restored instead of creating a conflicting duplicate.
4. **Given** a conversation is waiting, **When** its queue position changes, **Then** the participant sees the current status and position without refreshing.
5. **Given** a staff member sends a message, **When** delivery succeeds, **Then** the participant sees the message, sender identity, and send time in chronological order.
6. **Given** a conversation is closed, **When** the participant views it, **Then** message sending is disabled, a clear new-conversation action is available, and the participant can submit the conversation rating.
7. **Given** no live-support staff member is currently checked in for attendance, **When** a student or guest opens live support, **Then** starting a new conversation is disabled and the next configured support availability is shown.

---

### User Story 2 - Assign conversations fairly with capacity and queueing (Priority: P1)

The system automatically assigns each new conversation to an online support staff member with available capacity and the fewest active conversations. If all eligible staff members are full, the conversation waits in a first-in-first-out queue until capacity becomes available.

**Why this priority**: Automatic fair distribution prevents one staff member from being overloaded and ensures waiting users are served predictably.

**Independent Test**: Configure different capacities for two staff members, create enough conversations to fill and exceed capacity, close conversations, and verify least-load assignment, tie distribution, queue order, and automatic admission.

**Acceptance Scenarios**:

1. **Given** two online staff members where one has one active conversation and the other has none, **When** a new conversation arrives, **Then** it is assigned to the staff member with no active conversations.
2. **Given** eligible online staff members have equal active load and capacity, **When** multiple conversations arrive, **Then** assignments rotate fairly rather than repeatedly favoring one staff member.
3. **Given** every online staff member has reached the maximum configured capacity, **When** a new conversation arrives, **Then** it is placed at the end of the waiting queue and no staff member exceeds capacity.
4. **Given** conversations are waiting in order, **When** a staff member finishes an active conversation, **Then** the oldest eligible waiting conversation is assigned automatically and leaves the queue.
5. **Given** a staff member disconnects while owning active conversations, **When** the disconnection continues for two minutes, **Then** those conversations are safely redistributed or queued without message loss or duplicate ownership; reconnecting before two minutes preserves ownership.
6. **Given** an admin changes a staff member's capacity below the current active count, **When** the change is saved, **Then** existing conversations remain owned but no additional conversation is assigned until the active count falls below capacity.
7. **Given** a staff member has not checked in for attendance or has checked out, **When** a conversation needs assignment, **Then** that staff member is not eligible even if signed in to the platform.

---

### User Story 3 - Resolve student needs from one staff workspace (Priority: P1)

A support staff member sees the complete conversation beside the linked student's complete administrative profile and can perform every currently available student administration action from the same workspace. The workspace refreshes after each action so the staff member sees the resulting state immediately.

**Why this priority**: The central business outcome is resolving student issues without moving between disconnected administrative screens.

**Independent Test**: Open a linked student's conversation, inspect every profile category, execute representative read and write actions from each administration category, and verify success, failure, confirmation, refreshed state, and audit evidence.

**Acceptance Scenarios**:

1. **Given** a conversation linked to a student, **When** staff opens it, **Then** the workspace shows identity and contact details, account state, educational profile, packages and access grants, balance and transactions, devices, watch activity and limits, exams and homework state, overrides and requests, gamification, notes, CRM history, community or comment moderation context, and prior audit activity available for that student.
2. **Given** a student administration capability is available elsewhere in the current admin product, **When** the linked student is opened in live support, **Then** the equivalent capability is discoverable and executable from the live support workspace.
3. **Given** staff starts a sensitive action such as balance adjustment, package cancellation, password reset, account status change, device disconnection, watch-limit change, or student-account linking, **When** staff confirms it, **Then** the action executes once and the resulting student state is refreshed.
4. **Given** a student action fails validation or processing, **When** the failure occurs, **Then** no partial state is presented as successful and staff sees a specific recoverable error.
5. **Given** a guest conversation is not linked to a student, **When** staff opens the action area, **Then** student-specific data and actions remain unavailable until an explicit manual link is completed.

---

### User Story 4 - Manually link and correct guest identity (Priority: P2)

Staff can search for a student and explicitly link a guest conversation after verifying the visitor through the support process. The system clearly distinguishes visitor-supplied details from verified platform data and supports correcting an incorrect link with a complete audit trail.

**Why this priority**: Guests need support, but automatically exposing records based only on a typed phone number would disclose private student information.

**Independent Test**: Start a guest conversation, search for candidates, link one student, unlink or replace the link, and verify visibility boundaries and audit history throughout.

**Acceptance Scenarios**:

1. **Given** a guest supplied a phone number matching one or more accounts, **When** the conversation starts, **Then** no matching account details are revealed automatically.
2. **Given** staff has verified the visitor and selected a student account, **When** staff confirms the manual link, **Then** the workspace displays that student's data and records who created the link and when.
3. **Given** a conversation was linked incorrectly, **When** staff confirms unlinking or replacing the student, **Then** the prior link remains visible in the audit history and no data from the old student remains active in the workspace.
4. **Given** no suitable student exists, **When** staff chooses to create a student, **Then** the existing student-creation workflow is available from the workspace and the new student can be linked after successful creation.

---

### User Story 5 - Supervise operations and investigate every event (Priority: P2)

An admin monitors online staff, capacity, active conversations, queue state, transfers, timings, messages, and every student action. The admin can inspect a complete chronological record and intervene by transferring, reassigning, or closing a conversation.

**Why this priority**: Full staff access to student administration requires complete operational accountability and rapid intervention.

**Independent Test**: Run conversations through waiting, assignment, messaging, transfer, action execution, disconnect, and closure, then verify the admin dashboard and timeline contain each event with correct actors and durations.

**Acceptance Scenarios**:

1. **Given** live support activity exists, **When** the admin opens the dashboard, **Then** the admin sees online and unavailable staff, configured and consumed capacity, active conversations, waiting conversations, queue order, and current ownership.
2. **Given** a conversation changes state, **When** the admin opens its timeline, **Then** the timeline shows creation, queue entry and exit, assignments, transfers and reasons, messages and send times, student links, actions, failures, disconnects, new follow-up conversations, and closure in chronological order.
3. **Given** a completed conversation, **When** the admin reviews its metrics, **Then** wait duration, first-response duration, active handling duration, total elapsed duration, message counts, transfer count, and responsible staff members are available.
4. **Given** an admin transfers or closes a conversation, **When** the intervention succeeds, **Then** participants see the updated state and the intervention is auditable.
5. **Given** a staff member performs a student write action, **When** the admin inspects the audit entry, **Then** it identifies the staff member, conversation, student, action, time, reason where required, result, and safe before-and-after values.
6. **Given** participants have rated completed conversations, **When** the admin reviews staff performance, **Then** the admin sees rating counts and averages, with the same conversation rating attributed equally to every staff member who owned the conversation during its handling lifecycle.
7. **Given** a live conversation is open, **When** the admin opens it from the operations dashboard, **Then** the admin sees the durable message transcript beside the operational timeline and can send an identified admin message without becoming the assigned owner.
8. **Given** an admin sent a message during a previous shift, **When** a later support employee receives or opens the same conversation, **Then** that employee sees the admin message, sender role, and canonical send time in the preserved transcript.

---

### User Story 6 - Communicate safely during failures and concurrency (Priority: P3)

Participants and staff retain a coherent conversation when connections fail, messages are retried, two operators act at nearly the same time, or a student action takes longer than expected.

**Why this priority**: Support activity includes sensitive writes; duplicate messages, duplicate financial actions, or conflicting ownership would damage trust and data integrity.

**Independent Test**: Simulate reconnects, duplicate submissions, simultaneous assignment attempts, repeated sensitive-action confirmation, and temporary service failure, then verify one durable outcome and clear recovery states.

**Acceptance Scenarios**:

1. **Given** a sender retries the same message after an uncertain response, **When** the retry reaches the system, **Then** the conversation contains one logical message rather than duplicates.
2. **Given** two staff members attempt to take the same conversation, **When** both requests are processed, **Then** exactly one owner succeeds and the other sees the current owner.
3. **Given** a sensitive action is submitted repeatedly, **When** processing completes, **Then** the business change occurs at most once.
4. **Given** real-time updates are temporarily unavailable, **When** connectivity returns, **Then** missed messages and state changes are synchronized in correct order.

### Edge Cases

- A visitor enters an invalid, malformed, or unsupported phone number.
- A signed-in student already has an open conversation and tries to start another from a second tab or device.
- A guest session is cleared before the conversation finishes.
- No support staff member is online.
- A staff member is online but configured with zero available capacity.
- Capacity changes while assignments and closures are happening concurrently.
- A queued participant leaves and later reconnects after their turn was assigned.
- A staff member closes a conversation while a message or student action is still processing.
- The linked student is deactivated, deleted, merged, or otherwise becomes unavailable during the conversation.
- Staff attempts to link a conversation to a second student without first confirming replacement.
- A transfer target disconnects before accepting the transferred conversation.
- Very long histories, attachments, unsupported files, and failed uploads must not block current messages.
- Audit values containing passwords, tokens, secrets, or protected payment details must be redacted.
- Clock differences between participant devices must not change canonical event ordering or duration calculations.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: As a signed-in student, open live support, enter the queue, receive assignment, exchange messages, reconnect, and verify history and status continuity.
- **Manual QA Role/Flow 2**: As a guest, start with name and phone, verify no student data is exposed, then have staff manually link a student and perform representative administrative actions.
- **Manual QA Role/Flow 3**: As support staff, verify least-load assignment, configured capacity, automatic queue admission, transfer, closure, and complete linked-student workspace.
- **Manual QA Role/Flow 4**: As admin, verify operational dashboard, conversation timeline, metrics, capacity management, intervention controls, and action audit before/after values.
- **Manual QA Negative Check**: A guest who only knows a student's phone number must not receive or cause staff to see that student's private record until staff explicitly confirms a manual link.
- **Docker Acceptance**: The full compose stack starts healthy; schema changes apply cleanly on an existing database; backend, real-time messaging, all affected frontend surfaces, queue processing, and reverse proxy remain healthy after restart.
- **External Dependencies**: No WhatsApp or OTP provider is required. Full validation requires at least two staff accounts, one admin account, one student account, and one guest browser session.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a live support entry point for authenticated students and unauthenticated visitors on supported public and student surfaces.
- **FR-002**: The system MUST associate an authenticated student's new conversation with the authenticated student identity automatically.
- **FR-003**: The system MUST require a guest name and valid phone number before creating a guest conversation.
- **FR-004**: The system MUST NOT authenticate, verify, or automatically link a guest to a student account solely from the guest-supplied phone number.
- **FR-005**: The system MUST preserve conversation history and restore an existing open or waiting conversation when the same participant session reconnects.
- **FR-006**: The system MUST provide durable message identity, sender, canonical send time, delivery state, and chronological ordering.
- **FR-007**: The system MUST support participant and staff message delivery without requiring a page refresh.
- **FR-008**: The system MUST expose explicit waiting, assigned, active, transferred, closed, and abandoned conversation states where applicable; closed conversations MUST NOT return to an active state.
- **FR-009**: An admin MUST be able to configure the maximum number of active conversations independently for each support staff member.
- **FR-010**: The assignment process MUST consider only live-support staff whose attendance is currently checked in, whose support connection is online, and whose active load is below configured capacity; platform login alone MUST NOT make staff assignment-eligible.
- **FR-011**: The assignment process MUST select the eligible staff member with the fewest active conversations.
- **FR-012**: When eligible staff members have equal load, the assignment process MUST distribute conversations fairly through rotation.
- **FR-013**: The system MUST ensure that one conversation has no more than one responsible staff owner at a time.
- **FR-014**: The system MUST prevent new assignment from raising a staff member above configured capacity.
- **FR-015**: When no staff member has capacity, the system MUST place the conversation in a first-in-first-out waiting queue.
- **FR-016**: When capacity becomes available, the system MUST automatically assign the oldest eligible waiting conversation.
- **FR-017**: Reducing capacity below a staff member's current active count MUST preserve existing ownership and block additional assignments until load is below capacity.
- **FR-018**: The system MUST preserve ownership during a staff connection-loss grace period of two minutes, then safely redistribute or queue the staff member's conversations if the connection has not recovered.
- **FR-019**: Staff MUST be able to transfer a conversation to another eligible staff member and MUST provide a reason for transfer.
- **FR-020**: Closing a conversation MUST release one capacity slot and trigger waiting-queue admission.
- **FR-021**: A closed conversation MUST be read-only; the participant MUST start a distinct new conversation through an explicit action, while prior conversations remain available as history.
- **FR-022**: Every staff member enabled for live support MUST have complete student administration access inside the live support workspace, as explicitly approved by the product owner.
- **FR-023**: For a linked student, the workspace MUST display all student information categories and historical activity available in the current admin student experience.
- **FR-024**: For a linked student, the workspace MUST expose every current student administration action, including profile updates, notes, password reset, account activation or deactivation, device disconnection, package and access management, balance adjustment, watch-limit management, overrides and requests, gamification adjustment, academic state actions, and relevant moderation or CRM actions.
- **FR-025**: When a new student administration capability is added to the product's supported action catalog, it MUST be possible to surface that capability in the live support workspace without silently creating a reduced-access copy of the student profile.
- **FR-026**: Student-specific information and actions MUST remain unavailable for an unlinked guest conversation.
- **FR-027**: Staff MUST be able to search for a student and manually link an unlinked guest conversation after an explicit confirmation.
- **FR-028**: Staff MUST be able to create a student through the existing student-creation workflow and link the successfully created account.
- **FR-029**: Staff MUST be able to unlink or replace an incorrect student link only after explicit confirmation.
- **FR-030**: A student-link change MUST immediately stop active display of the previously linked student's information.
- **FR-031**: Sensitive student actions MUST show a confirmation that identifies the action, target student, intended change, and irreversible or financial impact before execution.
- **FR-032**: Repeated submission of the same message or sensitive action MUST produce at most one logical message or business change.
- **FR-033**: A failed student action MUST leave the student record consistent and MUST present a specific failure state to staff.
- **FR-034**: The workspace MUST refresh affected student information after a successful action without requiring staff to leave the conversation.
- **FR-035**: The admin dashboard MUST show staff online state, availability, configured capacity, active load, active conversations, waiting conversations, queue order, and ownership.
- **FR-036**: Admin MUST be able to inspect any live or historical support conversation and intervene through reassignment, transfer, or closure.
- **FR-037**: The system MUST maintain a chronological conversation timeline covering queue events, assignments, transfers, messages, participant and staff connection events, student-link changes, student actions, failures, closure, and references to later follow-up conversations.
- **FR-038**: The system MUST calculate wait time, first-response time, active handling time, total elapsed time, message counts, transfer counts, and responsible-staff history from canonical events.
- **FR-039**: Every state-changing staff or admin operation MUST record actor, target, conversation, canonical time, action, reason when required, outcome, and safe before-and-after values.
- **FR-040**: Audit records MUST redact passwords, authentication secrets, private tokens, and protected payment data.
- **FR-041**: Support staff MUST NOT be able to edit or remove messages, assignment history, metrics, or audit evidence in a way that erases accountability.
- **FR-042**: The system MUST record admin interventions distinctly from normal staff actions.
- **FR-043**: The system MUST preserve messages, ownership history, and audit events across temporary disconnects and service restarts.
- **FR-044**: The system MUST reconcile missed updates after reconnection without producing duplicate or incorrectly ordered events.
- **FR-045**: Staff and participants MUST receive clear loading, empty, waiting, reconnecting, failure, retry, and closed states appropriate to their current task.
- **FR-046**: Message history and student detail history MUST remain usable through pagination or progressive loading when records are large.
- **FR-047**: The admin MUST be able to identify conversations that were abandoned, repeatedly transferred, or exceeded operational response targets.
- **FR-048**: The feature MUST operate without WhatsApp or OTP integration.
- **FR-049**: Student and guest live support experiences MUST be usable on supported mobile screen sizes, while staff and admin workspaces MUST remain usable at their supported desktop and tablet sizes.
- **FR-050**: Keyboard navigation, focus visibility, semantic status announcements, and readable message/state contrast MUST support accessible operation of all critical live support flows.
- **FR-051**: After closure, the system MUST offer the participant a one-to-five-star rating associated with that completed conversation and its responsible staff history.
- **FR-052**: Admin performance reporting MUST attribute the same submitted conversation rating equally to every staff member who owned that conversation, include each staff member's rating count and average, and preserve the underlying conversation-level rating for investigation.
- **FR-053**: When no live-support staff member is currently checked in for attendance, the system MUST prevent creation of new conversations and show the next configured support availability derived from staff work schedules.
- **FR-054**: Conversations created before support becomes unavailable MUST remain durable and queued for the next eligible checked-in staff member rather than being deleted or automatically closed.
- **FR-055**: Admin MUST be able to open the durable transcript for every live conversation directly from the operations dashboard and send a message identified as originating from administration without changing current ownership.
- **FR-056**: Admin messages MUST remain in chronological conversation history so later-shift staff and reassigned owners see the same content, sender role, and canonical send time.
- **FR-057**: A distinct role permission MUST allow administrators to add every employee holding that role to conversation routing with a default capacity of one; adding or removing that permission MUST synchronize current role members without overwriting unrelated manual routing configuration.

### Key Entities

- **Support Conversation**: A durable support case with participant type, linked student when present, status, owner, queue position, timestamps, and closure outcome.
- **Support Participant**: The authenticated student or guest identity participating in a conversation, including guest-provided contact details and recognized session continuity.
- **Staff Availability**: A support staff member's online state, configured capacity, current active load, and assignment eligibility.
- **Conversation Assignment**: A time-bounded ownership record showing assignment source, responsible staff member, transfer reason, acceptance, and release.
- **Waiting Queue Entry**: A conversation's ordered waiting record, including entry time, current order, and reason for leaving the queue.
- **Support Message**: A uniquely identified message with conversation, sender, content or attachment reference, send time, and delivery state.
- **Student Conversation Link**: The explicit relationship between a guest conversation and a student, including who linked, replaced, or removed it and when.
- **Support Event**: A canonical chronological record used for operational timelines and duration calculations.
- **Student Action Execution**: A request to execute a supported administrative action from the conversation, with confirmation context, outcome, and safe before-and-after values.
- **Support Capacity Policy**: The admin-controlled maximum active conversation count for one staff member.
- **Conversation Rating**: A participant's post-closure star assessment linked to one completed conversation and the staff members responsible for it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In at least 95% of normal operating cases, a new conversation is assigned or visibly placed in the waiting queue within 3 seconds.
- **SC-002**: No staff member receives more active conversations than the configured maximum during concurrent-arrival tests.
- **SC-003**: When capacity becomes available, the oldest waiting conversation is admitted within 3 seconds in at least 95% of normal operating cases.
- **SC-004**: In at least 95% of normal connected sessions, a sent message becomes visible to the other participant with the correct sender and timestamp within 2 seconds.
- **SC-005**: All tested reconnect scenarios restore the same open conversation without message loss or conflicting duplicate ownership.
- **SC-006**: All tested sensitive student actions generate one business outcome and one complete audit entry even when the request is retried.
- **SC-007**: A guest who supplies another student's phone number receives no automatic account link or private student disclosure in all privacy-negative tests.
- **SC-008**: Staff can inspect the full linked-student context and complete representative actions from every existing student administration category without navigating away from the live support workspace.
- **SC-009**: Admin can reconstruct assignment, messages, transfers, student links, student actions, and closure for every tested conversation solely from its timeline and audit evidence.
- **SC-010**: Wait, first-response, handling, and total-duration metrics match canonical event times for all tested lifecycle paths.
- **SC-011**: All critical participant flows remain operable at a 320-pixel mobile width, and all critical staff controls remain keyboard accessible.
- **SC-012**: Temporary connection-loss tests recover without losing acknowledged messages, exceeding staff capacity, or applying a sensitive action twice.
- **SC-013**: Every completed conversation offers a rating flow, and submitted ratings appear in the responsible staff performance view with the correct conversation reference.

## Assumptions

- Support staff explicitly enabled for this feature receive the full student-control scope approved by the product owner; this intentionally does not inherit narrower existing assistant permissions.
- Admin configures staff capacity per employee; no product-wide fixed capacity is required.
- A staff member explicitly finishing or closing a conversation is the primary event that frees capacity.
- Assignment eligibility begins automatically when live-support staff records attendance check-in and ends when attendance check-out is recorded; there is no manual pause state during the attendance period, while connection loss is handled separately as a failure condition.
- Existing attendance work schedules are the source used to display the next support availability when nobody is checked in; if no future schedule exists, the interface states that support is unavailable without inventing a time.
- Guest name and phone are contact claims, not verified identity.
- Manual identity verification is an operational staff responsibility because OTP and WhatsApp are out of scope.
- Existing student administration business rules, validation, and financial invariants remain authoritative when actions are initiated from live support.
- Existing authentication, student profile, CRM, audit, and admin action sources remain the source of truth rather than creating separate duplicate student records.
- Attachments may use the platform's supported media rules; unsupported or unsafe files are rejected without blocking text chat.
- Historical support records follow the platform's standard administrative retention and privacy policy unless a later compliance requirement overrides it.

## Out of Scope

- WhatsApp, SMS, email, or third-party messaging-channel integration.
- OTP generation, delivery, or phone ownership verification.
- Chatbots, automated AI replies, sentiment analysis, or automated resolution decisions.
- Voice calls, video calls, or screen sharing.
- Allowing guests to self-search, view, or automatically claim student accounts by phone number.
- Replacing the underlying business rules of existing student administration actions.
