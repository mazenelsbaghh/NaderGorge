# API Contracts: AI Live Support Actions & Verification

This document defines the REST API endpoints exposed by the backend for participant confirmation, guest verification, secure registration, and confirmed human handoff.

## 1. Action Confirmation Endpoints

### Confirm Pending Action
- **Path**: `POST /api/live-support/participant/conversations/{conversationId}/ai/actions/{proposalId}/confirm`
- **Request Body**: None (payload and parameters are read from the pending action record on the server).
- **Response**: `200 OK`
  ```json
  {
    "success": true,
    "message": "Action executed successfully.",
    "executionResultJson": "{}"
  }
  ```
- **Error Codes**:
  - `404 Not Found` (Proposal or conversation not found).
  - `409 Conflict` (Proposal expired, already confirmed/canceled, or policy version revoked).

### Cancel Pending Action
- **Path**: `POST /api/live-support/participant/conversations/{conversationId}/ai/actions/{proposalId}/cancel`
- **Request Body**: None.
- **Response**: `200 OK`
  ```json
  {
    "success": true,
    "message": "Action proposal cancelled."
  }
  ```

---

## 2. Human Handoff Confirmation Endpoints

### Confirm Handoff
- **Path**: `POST /api/live-support/participant/conversations/{conversationId}/ai/handoff/confirm`
- **Request Body**: None.
- **Response**: `200 OK`
  ```json
  {
    "success": true,
    "message": "Conversation transferred to human support."
  }
  ```

### Cancel Handoff
- **Path**: `POST /api/live-support/participant/conversations/{conversationId}/ai/handoff/cancel`
- **Request Body**: None.
- **Response**: `200 OK`
  ```json
  {
    "success": true,
    "message": "Handoff cancelled. Returning to AI assistant."
  }
  ```

---

## 3. Guest Verification Endpoints

### Submit Lookup Key
- **Path**: `POST /api/live-support/participant/conversations/{conversationId}/ai/verification/lookup`
- **Request Body**:
  ```json
  {
    "lookupKey": "phone.full",
    "value": "01234567890"
  }
  ```
- **Response**: `200 OK` (Always constant/generic to prevent existence disclosure)
  ```json
  {
    "status": "Challenging",
    "nextQuestionKey": "profile.governorate",
    "promptText": "ما هي المحافظة المسجلة بحسابك؟"
  }
  ```

### Submit Challenge Answer
- **Path**: `POST /api/live-support/participant/conversations/{conversationId}/ai/verification/answer`
- **Request Body**:
  ```json
  {
    "answer": "الجيزة"
  }
  ```
- **Response**: `200 OK`
  ```json
  {
    "status": "Verified | Challenging | Exhausted",
    "nextQuestionKey": "contact.parent_phone_last4",
    "promptText": "اكتب آخر 4 أرقام من رقم هاتف ولي الأمر المسجل"
  }
  ```

---

## 4. Guest Account Registration Endpoint

### Confirm Registration Proposal
- **Path**: `POST /api/live-support/participant/conversations/{conversationId}/ai/account-proposal/confirm`
- **Request Body** (Secure fields):
  ```json
  {
    "fullName": "احمد محمد",
    "phoneNumber": "01234567890",
    "password": "SecurePassword123",
    "governorate": "القاهرة",
    "educationStage": "Primary",
    "gradeLevel": "Grade6",
    "schoolName": "مدرسة النيل",
    "parentPhoneNumber": "01098765432"
  }
  ```
- **Response**: `201 Created`
  ```json
  {
    "success": true,
    "message": "Account created and linked successfully.",
    "studentId": "GUID"
  }
  ```
