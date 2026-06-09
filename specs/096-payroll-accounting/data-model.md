# Data Model Design: Payroll, Teacher Finance, and Activated Code Accounting

## Enums

### 1. PayrollStatus
Represents the approval lifecycle of an employee payroll record:
- `Draft = 0`: Under preparation; additions and deductions can be modified.
- `Approved = 1`: Approved by Admin/Supervisor; locked and read-only.

### 2. PayrollAdjustmentType
Represents the direction of a payroll adjustment:
- `Addition = 0`: Bonus or extra compensation added to the basic salary.
- `Deduction = 1`: Salary reduction (e.g. absent days, delays, penalties).

### 3. PayoutStatus
Represents the status of a teacher payout request:
- `Pending = 0`: Requested by teacher, waiting for Admin review.
- `Paid = 1`: Approved and cash has been distributed; teacher's current balance is deducted.
- `Rejected = 2`: Declined by Admin; balance remains intact, rejection reason is recorded.

---

## Entities

### 1. PayrollRecord (Table: `payroll_records`)
Represents an employee's compensation breakdown for a specific month and year.

| Column | Type | Nullable | Constraints / Details |
|--------|------|----------|-----------------------|
| `Id` | Guid | No | Primary Key |
| `EmployeeProfileId`| Guid | No | FK to `employee_profiles` |
| `Month` | int | No | Range: 1 to 12 |
| `Year` | int | No | E.g. 2026 |
| `BasicSalary` | decimal | No | Precision (18,2). Copied from employee profile basic salary. |
| `Status` | int (enum)| No | Map to `PayrollStatus` (default: 0) |
| `ApprovedByUserId`| Guid | Yes | FK to `users` (Admin/Supervisor who approved it) |
| `ApprovedAt` | DateTime | Yes | Timestamp of approval |
| `CreatedAt` | DateTime | No | Default: UtcNow |
| `UpdatedAt` | DateTime | Yes | Updated on modification |

- **Calculated property (C# only)**: `NetSalary = BasicSalary + AdditionsSum - DeductionsSum`

### 2. PayrollAdjustment (Table: `payroll_adjustments`)
Represents individual additions/deductions applied to a specific payroll record.

| Column | Type | Nullable | Constraints / Details |
|--------|------|----------|-----------------------|
| `Id` | Guid | No | Primary Key |
| `PayrollRecordId`| Guid | No | FK to `payroll_records` (OnDelete: Cascade) |
| `Type` | int (enum)| No | Map to `PayrollAdjustmentType` |
| `Amount` | decimal | No | Precision (18,2). Must be greater than 0. |
| `Reason` | string | No | Max length: 2000. Text description of the adjustment. |
| `CreatedAt` | DateTime | No | Default: UtcNow |

### 3. TeacherAccount (Table: `teacher_accounts`)
Tracks teacher financial balances, earnings, and current commission rate.

| Column | Type | Nullable | Constraints / Details |
|--------|------|----------|-----------------------|
| `Id` | Guid | No | Primary Key |
| `TeacherId` | Guid | No | FK to `teacher_profiles` (Unique constraint) |
| `TotalEarnings` | decimal | No | Precision (18,2). Historical sum of all commissions earned. |
| `CurrentBalance`| decimal | No | Precision (18,2). Unpaid balance available for payout requests. |
| `CommissionRate`| decimal | No | Precision (18,2). Standard commission percentage (e.g. 0.70 for 70%). |
| `CreatedAt` | DateTime | No | Default: UtcNow |
| `UpdatedAt` | DateTime | Yes | Updated on transaction |

### 4. TeacherPayout (Table: `teacher_payouts`)
Represents a teacher's request to cash out their accumulated balance.

| Column | Type | Nullable | Constraints / Details |
|--------|------|----------|-----------------------|
| `Id` | Guid | No | Primary Key |
| `TeacherId` | Guid | No | FK to `teacher_profiles` |
| `Amount` | decimal | No | Precision (18,2). Requested cashout amount. |
| `Status` | int (enum)| No | Map to `PayoutStatus` (default: 0) |
| `RejectionReason`| string | Yes | Max length: 2000 characters. |
| `HandledByUserId`| Guid | Yes | FK to `users` (Admin/Supervisor who processed it) |
| `HandledAt` | DateTime | Yes | Timestamp of processing |
| `CreatedAt` | DateTime | No | Default: UtcNow |
| `UpdatedAt` | DateTime | Yes | Updated on modification |

### 5. AccessCodeActivationLog (Table: `access_code_activation_logs`)
Audit transaction log documenting a teacher's commission credit on student access code activation.

| Column | Type | Nullable | Constraints / Details |
|--------|------|----------|-----------------------|
| `Id` | Guid | No | Primary Key |
| `AccessCodeId` | Guid | No | FK to `access_codes` |
| `StudentId` | Guid | No | FK to `users` (Student who activated the code) |
| `PackageId` | Guid | Yes | FK to `packages` (nullable if code activated other resource types) |
| `TeacherId` | Guid | No | FK to `teacher_profiles` |
| `Price` | decimal | No | Precision (18,2). Price of content at activation (after discount). |
| `CommissionRate`| decimal | No | Precision (18,2). Active commission rate locked at activation time. |
| `CommissionEarned`| decimal| No | Precision (18,2). Price * CommissionRate. |
| `ActivatedAt` | DateTime | No | Default: UtcNow |

---

## Schema Modifications to Existing Tables

None. No fields need to be added to existing tables. We reuse the existing `EmployeeProfile`, `TeacherProfile`, `AccessCode`, and `CodeGroup` relationships.

---

## Indexes & Constraints

1. **`payroll_records`**:
   - Unique Index on `(EmployeeProfileId, Month, Year)` to prevent generating duplicate payroll entries for the same staff member in the same month.
   - Index on `ApprovedByUserId`.

2. **`payroll_adjustments`**:
   - Index on `PayrollRecordId`.

3. **`teacher_accounts`**:
   - Unique Index on `TeacherId` (representing a 1:1 relationship with `teacher_profiles`).

4. **`teacher_payouts`**:
   - Index on `TeacherId`.
   - Index on `Status`.

5. **`access_code_activation_logs`**:
   - Unique Index on `AccessCodeId` (since an access code can only be activated once).
   - Index on `TeacherId`.
   - Index on `StudentId`.
