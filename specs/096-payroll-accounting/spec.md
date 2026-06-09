# Feature Specification: Payroll, Teacher Finance, and Activated Code Accounting

**Feature Branch**: `096-payroll-accounting`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "Phase 9 - Payroll, Teacher Finance, and Activated Code Accounting"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Monthly Employee Payroll Management (Priority: P1)

As an Admin/Supervisor, I want to manage monthly payroll records for all employees/assistants, adding bonuses or deductions with clear reasons, and verifying them before final approval.

**Why this priority**: Crucial for tracking internal staff compensation, ensuring correct adjustments for bonuses/late clock-ins, and auditing net payouts.

**Independent Test**: Can be tested independently by logging in as Admin, generating monthly payroll for an assistant, adjusting deductions/additions, and locking it via approval.

**Acceptance Scenarios**:

1. **Given** an Admin is logged in, **When** they view the payroll dashboard, **Then** they see a list of monthly payroll entries for all employees showing basic salary, additions, deductions, net salary, and approval status.
2. **Given** an Admin is viewing a Draft payroll record, **When** they add a deduction of 150 EGP with reason "Unexcused absence", **Then** the net salary decreases by 150 EGP and the deduction details are persisted.
3. **Given** an Admin is viewing a Draft payroll record, **When** they click "Approve", **Then** the status transitions to Approved, a timestamp is set, and the payroll record becomes read-only.
4. **Given** an Assistant or Teacher user is logged in, **When** they attempt to access payroll list or update endpoints, **Then** the system rejects their request with a Forbidden error.

---

### User Story 2 - Teacher Account Balance & Commission Tracking (Priority: P1)

As a Teacher, I want to view my total earnings, current balance, commission rate, and a list of sales (activated codes/purchases) for my packages, so that I can track my revenue.

**Why this priority**: Allows teachers to self-service their financial reports, see exactly how much they earned, and verify commissions.

**Independent Test**: Can be tested by logging in as a Teacher, opening their finance tab, and checking total earnings and commission records.

**Acceptance Scenarios**:

1. **Given** a Teacher is logged in, **When** they open their finance dashboard, **Then** they see their total earnings, current balance, commission rate, and a list of transactions corresponding to student purchases/code activations of their courses.
2. **Given** a Teacher is logged in, **When** they attempt to view the finance dashboard of another teacher, **Then** the system rejects the request with a Forbidden error.
3. **Given** a Teacher is viewing transactions, **When** transaction prices are displayed, **Then** they are shown in EGP (real cash) and never mixed or converted to gamification points.

---

### User Story 3 - Teacher Payout Requests (Priority: P2)

As a Teacher, I want to request a payout from my current balance, and as an Admin, I want to review, approve, or reject these payout requests.

**Why this priority**: Enables the payout distribution workflow, maintaining a clear ledger of requests and payouts.

**Independent Test**: Can be tested by a teacher requesting a payout, which shows up as Pending on the Admin dashboard for approval/rejection.

**Acceptance Scenarios**:

1. **Given** a Teacher has a current balance of 1000 EGP, **When** they submit a payout request for 600 EGP, **Then** a payout request is created with status Pending, and the teacher's current balance remains unchanged until paid.
2. **Given** an Admin is reviewing a Pending payout request, **When** they approve it, **Then** the request status changes to Paid, and the teacher's current balance is deducted by the payout amount.
3. **Given** an Admin is reviewing a Pending payout request, **When** they reject it with reason "Invalid billing details", **Then** the request status changes to Rejected, the reason is saved, and the teacher's balance remains intact.

---

### User Story 4 - Activated Access Code Accounting & Reconciliations (Priority: P2)

As an Admin, I want to see detailed revenue and activation logs for access codes, grouping earnings by Teacher and Package, to reconcile payouts.

**Why this priority**: Required for auditing platform revenue versus teacher commission liability.

**Independent Test**: Can be tested by activating a code on a student account and checking the admin accounting logs to see the commission breakdown.

**Acceptance Scenarios**:

1. **Given** a student activates an access code for a package priced at 200 EGP, **When** the code is linked to a Teacher with a 70% commission rate, **Then** the teacher's account balance increases by 140 EGP (70%), total earnings increase by 140 EGP, and an audit transaction is logged.
2. **Given** an Admin is viewing the code accounting list, **When** reviewing transactions, **Then** they see a detailed log of code value, date activated, package name, teacher name, price, commission rate, and commission earned.

### Edge Cases

- **Payout Exceeding Balance**: If a teacher attempts to request a payout amount greater than their current balance, the system MUST reject the command with a validation error.
- **Historical Commission Rates**: If a teacher's commission rate is changed in `TeacherProfile`, all *previous* transactions and payouts must remain unaffected. Commission share is calculated and locked at the exact moment of code activation/purchase.
- **Double-Payout Prevention**: A payout request can only be approved/rejected once. Parallel requests to resolve must be blocked using concurrency tokens.
- **Locked Months**: Once a monthly payroll record is marked as `Approved`, no further additions, deductions, or basic salary modifications can be made.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Admin Flow**: Admin navigates to `/admin/media` -> `/admin/finance` tab, reviews list of employee payrolls, creates/adjusts a payroll record with an addition of 500 EGP for "Performance Bonus", and approves it.
- **Manual QA Teacher Flow**: Teacher logs in, navigates to `/teacher/finance`, views current balance, and submits a payout request.
- **Manual QA Negative Check**: Teacher or assistant tries to access the payroll page or endpoints of another user, receives HTTP 403.
- **Docker Acceptance**: Run docker-compose, run migrations, verify that the new tables (`payroll_records`, `teacher_accounts`, `teacher_payouts`, `access_code_activation_logs`) are created with proper indices.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support monthly `PayrollRecord` generation for staff/employees.
- **FR-002**: Payroll adjustments (additions and deductions) MUST require a text reason description.
- **FR-003**: System MUST calculate net salary as `BasicSalary + Additions - Deductions` and block adjustments once payroll is `Approved`.
- **FR-004**: System MUST maintain a `TeacherAccount` entity for each teacher profile tracking `TotalEarnings`, `CurrentBalance`, and `CommissionRate`.
- **FR-005**: Teachers MUST be able to request payouts from their `CurrentBalance`, creating a `TeacherPayout` entry in `Pending` status.
- **FR-006**: System MUST reject payout requests where amount is greater than the teacher's `CurrentBalance`.
- **FR-007**: System MUST record an `AccessCodeActivationLog` at the moment of code activation, calculating and locking the teacher commission share based on the rate active at that moment.
- **FR-008**: System MUST isolate all financial figures and show them exclusively in EGP (Egypt Pounds), keeping them separate from points.
- **FR-009**: System MUST restrict financial data endpoints so users can only view their own records (teachers view only their own accounts/payouts; assistants/students cannot view payroll or teacher finances).

### Key Entities

- **PayrollRecord**: Represents employee monthly compensation. Fields: Id, UserId (FK), Month, Year, BasicSalary, Additions, Deductions, NetSalary (calculated), Status (Draft/Approved), ApprovedById (FK), ApprovedAt, CreatedAt.
- **TeacherAccount**: Tracks teacher earnings. Fields: Id, TeacherId (FK to Users), TotalEarnings, CurrentBalance, CommissionRate, UpdatedAt.
- **TeacherPayout**: Represents payout requests. Fields: Id, TeacherId (FK to Users), Amount, Status (Pending/Paid/Rejected), RejectionReason, HandledById (FK), HandledAt, CreatedAt.
- **AccessCodeActivationLog**: Audit log for code activations. Fields: Id, AccessCodeId (FK), StudentId (FK), PackageId (FK), TeacherId (FK), Price, CommissionRate, CommissionEarned, ActivatedAt.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin/Supervisors can successfully run monthly payroll computations and approve payouts with 100% database transaction safety (no double deductions).
- **SC-002**: Financial balance totals are updated instantly (within 100ms of transaction) and displayed in Egyptian Pounds (EGP) format (e.g. `1,250.00 EGP`).
- **SC-003**: 100% of requests attempting to bypass role constraints or view other teachers' balances are blocked with HTTP 403 Forbidden.

## Assumptions

- **Commission Rates**: The initial commission rate for teachers is configured in `TeacherProfile` (e.g. 0.70 for 70%).
- **Payroll Period**: Payroll is calculated on a calendar month basis.
- **Currency**: EGP is the sole currency supported by the system.
- **Access Control**: Reuses existing role claims (`Admin`, `Supervisor`, `Teacher`, `Assistant`, `Student`).
