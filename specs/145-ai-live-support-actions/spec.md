# Feature Specification: AI Live Support Actions & Verification

**Feature Branch**: `145-ai-live-support-actions`  
**Created**: 2026-06-23  
**Status**: Draft  
**Input**: User-approved Feature Brief: "تفعيل أدوات وإجراءات المساعد الذكي والتحقق من حسابات الزوار"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Execute Allowed Actions after Confirmation (Priority: P1)

An authenticated student asks the AI to perform a change on their account (e.g., unlocking a lesson, disconnecting active devices, or approving a watch request). The AI proposes the action to the student by showing a confirmation card. The student must explicitly confirm the action, after which the backend executes it securely.

**Why this priority**: Core value of an agentic support system. Enables resolving student problems automatically and securely without staff intervention.

**Independent Test**: Enable the "Unlock Lesson" action in the admin policy. Ask the AI to unlock a locked lesson, receive the confirmation card, click "Confirm", and verify that the lesson is unlocked in the database.

**Acceptance Scenarios**:

1. **Given** the "Unlock Lesson" action is enabled in the active AI policy, **When** a student requests to open a lesson, **Then** the AI proposes the action, displaying a card with the lesson name, intended effect, and confirm/cancel buttons.
2. **Given** a pending action proposal card is displayed, **When** the student does nothing or continues chatting without clicking confirm, **Then** no database change is executed.
3. **Given** the student clicks "نعم، متأكد" (Confirm) on a valid pending action card, **When** the backend verifies that the action is still allowed and the token is not expired, **Then** the action executes successfully, the lesson is unlocked, and the transaction is logged.
4. **Given** a student attempts to confirm an action that has expired (duration configured by the admin), **When** they click confirm, **Then** the UI displays an expiration message and no action is executed.
5. **Given** a student clicks "إلغاء" (Cancel) on the proposal card, **When** the backend marks the action canceled, **Then** the card becomes inactive, no change is made, and the AI continues the conversation.

---

### User Story 2 - Confirm Human Support Handoff (Priority: P1)

When the AI decides to hand off the conversation to a human support agent (e.g., because it cannot find the answer in the knowledge base, or the student explicitly requests a human), it must propose the handoff. The student is presented with a card to confirm the handoff. AI remains active if the handoff is rejected.

**Why this priority**: Prevents premature or unwanted handoffs, reducing human staff load and keeping students in the self-service flow.

**Independent Test**: Student asks to speak with a human support agent. The AI displays a handoff confirmation card. The student declines, and the AI continues responding to subsequent questions normally.

**Acceptance Scenarios**:

1. **Given** the AI decides to hand off the conversation, **When** it triggers the handoff decision, **Then** a confirmation card is displayed to the student: *"يريد المساعد الذكي تحويلك لموظف بشري للمساعدة. هل تريد التحويل؟"* with "Yes, transfer me" and "No, continue with assistant" buttons.
2. **Given** the handoff confirmation card is active, **When** the student clicks "نعم، حوّلني" (Yes, transfer me), **Then** the conversation status transitions to `HumanQueued` or `HumanAssigned`, and AI is disabled.
3. **Given** the handoff confirmation card is active, **When** the student clicks "لا، استمر مع المساعد" (No, continue with assistant), **Then** the conversation remains active with the AI, a hidden message is sent to the AI's history informing it of the rejection, and the AI continues responding.
4. **Given** an automatic lockout occurs (e.g., student fails verification challenges 3 times) OR a critical technical/provider failure occurs, **When** the system initiates a handoff, **Then** the conversation transitions to human support immediately without asking for student confirmation, ensuring the user is not stuck.

---

### User Story 3 - Secure Guest Account Creation & Verification (Priority: P2)

A guest can receive general help, or verify an existing account by matching lookup keys (e.g., phone number) and answering challenge questions (e.g., governorate, parent's phone) to link their conversation. Alternatively, a guest can register a new account through a secure form displayed directly in the chat widget.

**Why this priority**: Essential for supporting unregistered users and providing safe self-service recovery or registration.

**Independent Test**: Open a chat as a guest. Ask to register a new account. Fill the secure form in the widget, click submit, and verify that the student account is created and the conversation is linked to it.

**Acceptance Scenarios**:

1. **Given** a guest asks to register a new account, **When** the AI proposes account creation, **Then** the student widget displays a secure registration form (collecting name, phone, password, governorate, grade level, school, parent phone, etc.).
2. **Given** the guest fills and submits the secure form, **When** the backend validates the inputs, **Then** it creates the student profile, links the conversation, and logs the user in, while keeping sensitive fields (like passwords) out of the AI transcript and provider context.
3. **Given** a guest claims to have an existing account, **When** they initiate account recovery, **Then** the AI asks for a complete lookup key (e.g., full phone number), and the backend checks for a match without disclosing candidate identity or existence hints.
4. **Given** a candidate account is matched, **When** the system challenges the user with configured questions, **Then** the guest must answer them correctly. Correct answers link the conversation; exceeding the maximum attempts hands off to human support immediately.

---

### Edge Cases

- **Configuration Revoked mid-conversation**: If the admin disables an action key (e.g. unlocks) in the policy settings while a student has a pending confirmation card open in their browser, the backend must reject the execution when they click confirm.
- **Raced Messages during Handoff Proposal**: If the student sends a text message while a handoff proposal card is active, the AI must not reply until the student resolves the handoff proposal (by accepting or declining it).
- **Double Clicks on Confirmation**: Rapid duplicate clicks on "Confirm" must trigger exactly-once execution.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Admin Config**: Log in as Admin, go to `/admin/live-support/ai`. Check "فك قفل الحصص" (Unlock Lesson) under actions. Save and publish.
- **Manual QA Student Flow**: Start chat, ask to open a locked lesson. Verify confirmation card appears with clear details. Click confirm and check if the lesson becomes unlocked.
- **Manual QA Guest Flow**: Start chat as guest, request to register a new account. Fill the form, verify creation, and verify that the password did not leak to the AI chat history.
- **Docker Acceptance**: Apply all migrations, compile and rebuild backend and worker containers, verify health status.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The C# backend and Node worker MUST support all 6 decision types: `reply`, `propose_action`, `request_verification`, `propose_account_creation`, `request_resolution`, and `handoff`.
- **FR-002**: The backend MUST dynamically build the AI system instructions by appending the enabled action keys and their argument schemas from the active published policy.
- **FR-003**: The backend MUST build a student profile context document containing only the database fields allowed by the active policy's readable data keys, formatting it as a distinct context document for the worker.
- **FR-004**: All proposed actions and handoffs proposed by the AI MUST require explicit participant confirmation in the student UI before execution, unless forced by technical failures or lockout.
- **FR-005**: The UI MUST render interactive confirmation cards for actions, handoffs, and a secure form for registration.
- **FR-006**: Sensitive registration inputs (e.g., password) MUST be transmitted securely to the backend API and MUST never be saved in the AI chat history or sent to the Gemini API.

### Key Entities *(include if feature involves data)*

- **LiveSupportAIPendingAction**: Stores the pending action proposal, including type/key, arguments JSON, expiry, status (Pending, Confirmed, Cancelled, Succeeded, Failed), and completed timestamp.
- **LiveSupportAIVerificationSession**: Stores the guest verification status, matched candidate ID (redacted/secure), current question key, and attempts count.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Confirmed actions are executed on the backend and reflected in the UI in under 1.5 seconds.
- **SC-002**: Denying a handoff returns the AI to active status immediately (< 500ms).
- **SC-003**: 100% of registration password inputs are successfully excluded from database transcripts and worker requests.

## Assumptions

- **Existing business logic**: The AI execution layer will invoke existing MediatR commands (e.g., `ManualUnlockCommand`, `DisconnectStudentDeviceCommand`) and respect all existing business constraints.
- **Cairo typography**: All new UI components in the student widget will use the system's Cairo/RTL styles.
