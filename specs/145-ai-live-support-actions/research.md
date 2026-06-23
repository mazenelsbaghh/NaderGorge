# Research: AI Live Support Actions & Verification

This document analyzes the architecture, database states, worker schemas, and frontend rendering required to implement confirmed actions, guest verification, secure registration, and confirmed handoffs.

## 1. AI Decision Schema & Worker Integration

### Decision Types
The Gemini worker must support the following JSON structure when generating live-support decisions:
- `reply`: A standard chat reply.
- `propose_action`: AI proposes an administrative action (e.g. unlocking a lesson).
- `request_verification`: AI requests verification challenges for a guest.
- `propose_account_creation`: AI proposes creating a new account for a guest.
- `handoff`: AI requests a handoff to human support.

### Worker Schema
In `worker/src/services/geminiService.ts`, `liveSupportDecisionSchema` will be updated to:
```typescript
const liveSupportDecisionSchema = {
  type: Type.OBJECT,
  properties: {
    schemaVersion: { type: Type.STRING },
    type: { type: Type.STRING, enum: ['reply', 'propose_action', 'request_verification', 'propose_account_creation', 'handoff'] },
    messageAr: { type: Type.STRING },
    action: {
      type: Type.OBJECT,
      properties: {
        key: { type: Type.STRING },
        arguments: { type: Type.OBJECT },
        safeEffectSummaryAr: { type: Type.STRING }
      },
      required: ['key', 'safeEffectSummaryAr']
    },
    verification: {
      type: Type.OBJECT,
      properties: {
        intent: { type: Type.STRING }
      },
      required: ['intent']
    },
    accountCreation: {
      type: Type.OBJECT,
      properties: {
        requestedFields: { type: Type.ARRAY, items: { type: Type.STRING } }
      },
      required: ['requestedFields']
    },
    handoff: {
      type: Type.OBJECT,
      properties: {
        reasonCode: { type: Type.STRING },
        safeSummaryAr: { type: Type.STRING }
      },
      required: ['reasonCode', 'safeSummaryAr']
    }
  },
  required: ['schemaVersion', 'type']
};
```

## 2. Dynamic Policy Instruction & Context Building

### Action Catalogs Injection
When the worker claims a turn (`ClaimAITurnAsync`), the C# backend will dynamically append description text to `policy.SystemInstructions` based on the enabled keys in `policy.ActionKeysJson`:
- Iterate over enabled action keys.
- Match them with descriptions and expected JSON arguments from `LiveSupportAICatalog.Actions`.
- Append this configuration to the system instructions so the model is aware of the exact schema parameters for each action.

### Student Profile Context Document
If the conversation is linked to a student (i.e. `conversation.LinkedStudentUserId` is not null), the backend will dynamically build a `--- STUDENT PROFILE CONTEXT ---` document:
- Match enabled read keys in `policy.ReadableDataKeysJson` (e.g. `devices.summary`, `access.grants`).
- Query corresponding databases (e.g. `StudentAccessGrants`, `Devices`, `LessonProgresses`).
- Format as human-readable structured JSON or key-value list (redacting sensitive hashes or tokens).
- Append it to the `knowledgeDocs` returned in `LiveSupportAITurnContextDto`.

## 3. Confirmed Handoff Flow

### Normal Handoff (Confirmed)
When the AI decides to hand off (type = `"handoff"`), the backend will:
1. Save the proposed handoff summary and reason in a new record or inside the pending actions list.
2. Maintain the conversation status as `AiActive`.
3. Return the handoff proposal to the student UI.
4. Render a card in the student UI: *"يريد المساعد الذكي تحويلك لموظف بشري للمساعدة في: [ملخص]. هل تريد التحويل؟"*.
5. If the student clicks **"نعم، حوّلني"**:
   - Call `POST /api/live-support/participant/conversations/{id}/confirm-handoff`.
   - Backend transitions status to `HumanQueued` and queues the conversation.
6. If the student clicks **"لا، استمر مع المساعد"**:
   - Call `POST /api/live-support/participant/conversations/{id}/cancel-handoff`.
   - Backend marks the handoff proposal canceled, adds a hidden event/message to the history: `[System] رفض الطالب التحويل للدعم البشري ويريد الاستمرار في التحدث معك`.
   - Conversation remains `AiActive` and AI continues.

### Immediate Handoff (Forced)
Bypasses the confirmation card and transitions the conversation directly to `HumanQueued`:
- **Verification Exhaustion**: If the guest fails the challenge verification 3 times.
- **AI Technical Error**: BullMQ job failures, Gemini timeouts, or validation exceptions.

## 4. Secure Guest Account Creation & Verification

### Registration
- AI returns `propose_account_creation`.
- Widget renders a full registration form (collecting name, phone, governorate, grade level, school, parent phone, and password).
- Form submits directly to a secure endpoint `POST /api/live-support/participant/conversations/{id}/confirm-register` which validates the inputs, creates the student user using the existing UserManager, and links the conversation.

### Verification
- Matched via `phone.full` or `student_code.full`.
- Backend selects challenge questions from the candidate profile (e.g., matching governorate, parent phone's last 4 digits).
- The answers are normalization-compared in-memory. Expected answers are never sent to the worker.
