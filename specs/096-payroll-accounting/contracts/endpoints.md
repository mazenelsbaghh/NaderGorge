# API Endpoints: Payroll, Teacher Finance, and Activated Code Accounting

All payroll and administrative payouts endpoints are protected and require the user to have either an `Admin` or `Supervisor` role. Assistants and students must be blocked.
All teacher finance endpoints are protected and require the user to have the `Teacher` role, with data isolation strictly enforced.

---

## 1. Administrative Payroll Management

### GET `/api/admin/finance/payroll`
Lists payroll records for all employees for a specific month and year.

**Parameters**:
- `month` (int, required): 1-12
- `year` (int, required): e.g. 2026

**Response (200 OK)**:
```json
{
  "success": true,
  "data": [
    {
      "id": "a5b7b99c-e35b-4c5c-9c9c-9c9c9c9c9c9c",
      "employeeProfileId": "b1b2c3d4-e35b-4c5c-9c9c-9c9c9c9c9c9c",
      "employeeName": "Assistant Name",
      "month": 6,
      "year": 2026,
      "basicSalary": 5000.00,
      "additions": 450.00,
      "deductions": 150.00,
      "netSalary": 5300.00,
      "status": "Draft",
      "approvedByUserId": null,
      "approvedByName": null,
      "approvedAt": null,
      "createdAt": "2026-06-09T08:00:00Z"
    }
  ]
}
```

### POST `/api/admin/finance/payroll/generate`
Generates draft payroll records for all employees/assistants for a specific month and year. Assumes the employee profile `basicSalary` is used.

**Request Body**:
```json
{
  "month": 6,
  "year": 2026
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "message": "Generated 5 payroll records successfully.",
  "data": 5
}
```

### POST `/api/admin/finance/payroll/{id}/adjustments`
Adds an addition or deduction to a Draft payroll record. Returns bad request if payroll is already Approved.

**Request Body**:
```json
{
  "type": 0, // 0 = Addition, 1 = Deduction
  "amount": 250.00,
  "reason": "Outstanding performance bonus"
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "message": "Payroll adjustment added successfully.",
  "data": {
    "id": "c1c2c3d4-e35b-4c5c-9c9c-9c9c9c9c9c9c",
    "type": "Addition",
    "amount": 250.00,
    "reason": "Outstanding performance bonus",
    "createdAt": "2026-06-09T08:05:00Z"
  }
}
```

### DELETE `/api/admin/finance/payroll/{id}/adjustments/{adjustmentId}`
Deletes a specific payroll adjustment from a Draft payroll record.

**Response (200 OK)**:
```json
{
  "success": true,
  "message": "Adjustment deleted successfully."
}
```

### POST `/api/admin/finance/payroll/{id}/approve`
Approves a payroll record, locking it from future adjustments.

**Response (200 OK)**:
```json
{
  "success": true,
  "message": "Payroll record approved and locked successfully."
}
```

---

## 2. Teacher Payout Reviews (Admin/Supervisor)

### GET `/api/admin/finance/payouts`
Lists all teacher payout requests.

**Parameters**:
- `status` (int, optional): Filter by `PayoutStatus` enum (0 = Pending, 1 = Paid, 2 = Rejected)

**Response (200 OK)**:
```json
{
  "success": true,
  "data": [
    {
      "id": "d1d2c3d4-e35b-4c5c-9c9c-9c9c9c9c9c9c",
      "teacherId": "t1b2c3d4-e35b-4c5c-9c9c-9c9c9c9c9c9c",
      "teacherName": "Nader George",
      "amount": 1500.00,
      "status": "Pending",
      "rejectionReason": null,
      "handledByUserId": null,
      "handledByName": null,
      "handledAt": null,
      "createdAt": "2026-06-09T08:10:00Z"
    }
  ]
}
```

### POST `/api/admin/finance/payouts/{id}/resolve`
Approves (marks as Paid) or Rejects a payout request. Rejection requires a reason. Concurrency tokens prevent double-processing.

**Request Body**:
```json
{
  "status": 1, // 1 = Paid, 2 = Rejected
  "rejectionReason": "Invalid billing details" // required if status is 2
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "message": "Payout request status updated to Paid."
}
```

---

## 3. Teacher Self-Service Finance (Teacher role)

### GET `/api/teacher/finance/account`
Gets the logged-in teacher's account balance, total earnings, and commission rate.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "teacherId": "t1b2c3d4-e35b-4c5c-9c9c-9c9c9c9c9c9c",
    "teacherName": "Nader George",
    "totalEarnings": 12500.00,
    "currentBalance": 4200.00,
    "commissionRate": 0.70
  }
}
```

### GET `/api/teacher/finance/transactions`
Lists access code activations for the logged-in teacher's packages.

**Parameters**:
- `page` (int, default: 1)
- `pageSize` (int, default: 20)

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "f1f2c3d4-e35b-4c5c-9c9c-9c9c9c9c9c9c",
        "packageName": "Physics Term 2 Pack",
        "studentName": "Student Full Name",
        "serialNumber": 10002304,
        "price": 200.00,
        "commissionRate": 0.70,
        "commissionEarned": 140.00,
        "activatedAt": "2026-06-09T08:15:00Z"
      }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 20
  }
}
```

### GET `/api/teacher/finance/payouts`
Lists payout request history for the logged-in teacher.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": [
    {
      "id": "d1d2c3d4-e35b-4c5c-9c9c-9c9c9c9c9c9c",
      "amount": 1500.00,
      "status": "Paid",
      "rejectionReason": null,
      "createdAt": "2026-06-09T08:10:00Z",
      "handledAt": "2026-06-09T08:20:00Z"
    }
  ]
}
```

### POST `/api/teacher/finance/payouts`
Submits a new payout request. Amount cannot exceed current balance.

**Request Body**:
```json
{
  "amount": 1000.00
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "message": "Payout request submitted successfully.",
  "data": {
    "id": "d2d2c3d4-e35b-4c5c-9c9c-9c9c9c9c9c9c",
    "amount": 1000.00,
    "status": "Pending",
    "createdAt": "2026-06-09T08:30:00Z"
  }
}
```

---

## 4. Reconciliations & Code Accounting (Admin/Supervisor)

### GET `/api/admin/finance/code-accounting`
Lists access code activations across all teachers and packages for reconciliation.

**Parameters**:
- `teacherId` (Guid, optional): Filter by teacher
- `packageId` (Guid, optional): Filter by package
- `startDate` (DateTime, optional)
- `endDate` (DateTime, optional)
- `page` (int, default: 1)
- `pageSize` (int, default: 20)

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "f1f2c3d4-e35b-4c5c-9c9c-9c9c9c9c9c9c",
        "packageName": "Physics Term 2 Pack",
        "teacherName": "Nader George",
        "studentName": "Student Full Name",
        "serialNumber": 10002304,
        "price": 200.00,
        "commissionRate": 0.70,
        "commissionEarned": 140.00,
        "activatedAt": "2026-06-09T08:15:00Z"
      }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 20
  }
}
```
