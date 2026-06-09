# API Contracts: Call Center CRM

All endpoints are prefix-routed under `api/crm` and require an authenticated user with `crm.manage` permission.

---

## 1. `GET /api/crm/students`
Fetches a paginated list of students and their current CRM follow-up statuses.

### Query Parameters
- `page` (int, default: 1)
- `pageSize` (int, default: 20)
- `search` (string, optional)
- `status` (CrmStatus enum, optional)
- `agentId` (Guid, optional) - *Ignored for regular agents (overridden with their own ID)*
- `priority` (CrmPriority enum, optional)
- `onlyOverdue` (bool, default: false) - *Filter for students whose next follow-up date is in the past*

### Response (200 OK)
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "studentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "studentName": "احمد محمد",
        "studentPhone": "01011111111",
        "crmStatus": "InProgress",
        "assignedAgentId": "8fa85f64-5717-4562-b3fc-2c963f66afa6",
        "assignedAgentName": "أ. عمر (مساعد)",
        "priority": "High",
        "nextFollowUpDate": "2026-06-10T12:00:00Z",
        "lastCalledAt": "2026-06-08T15:30:00Z",
        "notes": "الوالد طلب معاودة الاتصال يوم الأربعاء"
      }
    ],
    "totalCount": 145,
    "page": 1,
    "pageSize": 20
  }
}
```

---

## 2. `POST /api/crm/students/{studentId}/assign`
Assigns or reassigns a student to a call center agent, sets follow-up priority, and registers core notes. (Restricted to Admin/Supervisor roles).

### Request Payload
```json
{
  "assignedAgentId": "8fa85f64-5717-4562-b3fc-2c963f66afa6",
  "priority": "High",
  "notes": "الرجاء المتابعة بخصوص كورس الفيزياء"
}
```

### Response (200 OK)
```json
{
  "success": true,
  "message": "Student successfully assigned to agent."
}
```

---

## 3. `POST /api/crm/students/{studentId}/calls`
Logs a call interaction outcome and schedules the next follow-up window.

### Request Payload
```json
{
  "outcome": "NoAnswer",
  "notes": "الرقم مغلق، سيتم المحاولة لاحقاً",
  "nextFollowUpDate": "2026-06-09T18:00:00Z"
}
```

### Response (200 OK)
```json
{
  "success": true,
  "message": "Call log recorded successfully."
}
```

---

## 4. `GET /api/crm/students/{studentId}/history`
Retrieves chronological call log history logs for a specific student.

### Response (200 OK)
```json
{
  "success": true,
  "data": [
    {
      "id": "e00a98f4-4dfc-42cb-b169-2f22c5432101",
      "studentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "agentName": "أ. عمر (مساعد)",
      "callDate": "2026-06-08T15:30:00Z",
      "outcome": "NoAnswer",
      "notes": "الجرس يرن ولا يوجد رد",
      "nextFollowUpDate": "2026-06-09T10:00:00Z"
    }
  ]
}
```

---

## 5. `GET /api/crm/reports/performance`
Retrieves CRM performance analytics, call volume metrics, and agent task status overview. (Restricted to Admin/Supervisor roles).

### Response (200 OK)
```json
{
  "success": true,
  "data": {
    "totalCalls": 450,
    "outcomeBreakdown": {
      "Completed": 180,
      "Pending": 40,
      "NoAnswer": 150,
      "Postponed": 60,
      "Closed": 20
    },
    "agentPerformance": [
      {
        "agentId": "8fa85f64-5717-4562-b3fc-2c963f66afa6",
        "agentName": "أ. عمر (مساعد)",
        "callsMade": 120,
        "completedCalls": 55,
        "noAnswerCalls": 40
      }
    ]
  }
}
```
