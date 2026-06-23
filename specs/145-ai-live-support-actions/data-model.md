# Data Model: AI Live Support Actions & Verification

All required data tables and enums are already present in the database from the baseline migrations. No database schema changes or EF Core migrations are required for this phase.

## Entities Summary

### 1. LiveSupportAIPendingAction
Stores the pending action proposed by the AI and awaits student confirmation:
- `ConversationId` (Guid): The conversation this action is proposed in.
- `ActionKey` (string): The key of the action (e.g. `student.lesson.unlock`).
- `ArgumentsJson` (string): JSON payload for the action args (e.g. `{"lessonId": "..."}`).
- `SafeEffectSummary` (string): Description of the action's intended effect in Arabic.
- `Status` (LiveSupportAIPendingActionStatus): Status enum (PendingConfirmation, Confirmed, Cancelled, Expired, Succeeded, Failed).
- `ExpiresAt` (DateTime): Expiration timestamp.
- `CompletedByUserId` (Guid?): User who executed/cancelled the action.
- `CompletedAt` (DateTime?): Completed timestamp.

### 2. LiveSupportAIVerificationSession
Represents a guest verification session linked to a conversation:
- `ConversationId` (Guid): The conversation ID.
- `PolicyVersionId` (Guid): The active policy version used.
- `CandidateStudentUserId` (Guid?): Redacted candidate user matched.
- `LookupKey` (string): Key used for lookup (e.g., `phone.full`).
- `LookupValueHash` (string): SHA-256 hash of the lookup value (for security/privacy).
- `SelectedQuestionKeysJson` (string): JSON array of selected verification questions.
- `RequiredCorrect` (int): Number of correct answers needed.
- `CorrectCount` (int): Number of correct answers matched so far.
- `AttemptCount` (int): Number of verification attempts made.
- `MaxAttempts` (int): Max attempts allowed before human handoff.
- `Status` (LiveSupportAIVerificationStatus): Status enum (Challenging, Verified, Failed, Exhausted).
- `ExpiresAt` (DateTime): Expiration timestamp.

### 3. LiveSupportAIVerificationAttempt
Tracks individual verification answer submissions:
- `SessionId` (Guid): The linked session.
- `QuestionKeysJson` (string): Questions asked in this attempt.
- `OutcomeCodesJson` (string): Outcomes of each answer (Correct, Incorrect).
- `SubmittedAt` (DateTime): Submission timestamp.
- `AttemptNumber` (int): Attempt number.

## Enums Used

- `LiveSupportAIDecisionType`: `Reply`, `ProposeAction`, `RequestVerification`, `ProposeAccountCreation`, `RequestResolution`, `Handoff`.
- `LiveSupportAIPendingActionStatus`: `PendingConfirmation`, `Confirmed`, `Cancelled`, `Expired`, `Invalidated`, `Executing`, `Succeeded`, `Failed`.
- `LiveSupportAIVerificationStatus`: `AwaitingLookup`, `Challenging`, `Verified`, `Failed`, `Exhausted`, `Ambiguous`, `Cancelled`, `HandedOff`.
