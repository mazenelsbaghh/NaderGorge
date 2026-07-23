# Data Model: منظومة الموارد البشرية المتكاملة

## قواعد عامة

- كل كيان mutable يرث timestamps ويدعم optimistic concurrency.
- جميع اللحظات UTC؛ `WorkDate` وcalendar decisions بتوقيت القاهرة.
- كل EmployeeProfile يرتبط بـUser واحد موجود؛ `UserId` فريد وغير nullable، ولا cascade delete.
- كل السجلات المالية وقرارات الموافقة والتاريخ/audit تستخدم `Restrict` أو archival references.
- الجداول المؤرخة تستخدم `EffectiveFrom` شامل و`EffectiveTo` غير شامل، ولا تسمح بتداخل فعال لنفس النطاق.
- العمليات الحساسة تحمل `CorrelationId` و`IdempotencyKey` فريدين في نطاق operation+actor.

## Identity & Organization Aggregate

### EmployeeProfile

`Id`, `UserId`, `EmployeeNumber`, `EmploymentStatus`, `HireDate`, `TerminationDate?`, `WorkMode`, `PrimaryLocationId?`, `CurrentAssignmentId?`, `CreatedAt`, `UpdatedAt`, `RowVersion`.

Constraints: unique `UserId`; unique `EmployeeNumber`; no profile without User; teacher/student needs explicit hire command; status transition Active→Suspended/OnLeave/Terminated/Archived with reason.

### OrganizationUnit

`Id`, `Type` (Branch/Department/Section/Team), `Name`, `Code`, `ParentId?`, `ManagerEmployeeId?`, `IsActive`, effective dates.

Constraints: single-company tree; no cycles; sibling code unique; manager must be active in effective period.

### JobPosition / JobGrade / WorkLocation / CostCenter

Reference entities with code, name, active state and effective dates. WorkLocation contains timezone/calendar and optional geofences.

### EmploymentAssignment

`EmployeeId`, `OrganizationUnitId`, `PositionId`, `GradeId?`, `ManagerEmployeeId?`, `LocationId?`, `CostCenterId?`, `EffectiveFrom`, `EffectiveTo?`, `Reason`.

Constraints: no overlapping primary assignment; manager cannot equal employee; manager scope resolves by effective date.

### EmploymentContract

`EmployeeId`, `ContractNumber`, `Type`, `StartDate`, `EndDate?`, `ProbationEndDate?`, `WorkTerms`, `BaseSalary`, `Currency`, `Status`, effective/version metadata.

Transitions: Draft→Active→Renewed/Expired/Suspended/Terminated. Activation requires employee/account/assignment.

## Shift & Attendance Aggregate

### WorkCalendar / Holiday

Calendar by location with weekly off-days and holiday rows; prevents duplicate dates in a calendar.

### ShiftTemplate / ShiftSegment

Template contains mode, grace, overtime, missing-clock policy and attendance policy. Segments store start/end offsets and breaks, allowing overnight and split shifts.

### ShiftAssignment

`EmployeeId`, `ShiftTemplateId`, `EffectiveFrom`, `EffectiveTo`, `Source`, `PublishedAt?`; no overlapping published assignments. Swaps retain original links.

### AttendancePolicy / TrustedDevice / RemoteWorkException

Policy mode Unrestricted/Geofence/TrustedDevice; device stores hashed token, label, approved/revoked dates; remote exception is effective-dated and approval-linked.

### AttendanceAttempt

Immutable evidence: employee, timestamp, action, accepted, reason code, IP, user agent, coordinates/accuracy/distance, trusted device id, resolved policy/version, correlation id.

### AttendanceSession / AttendanceBreak

Session: employee, shift assignment, WorkDate, clock-in/out UTC, status, computed late/early/overtime/worked minutes, source and correction version. Breaks are bounded intervals inside session.

Constraints: one active session per employee; no overlapping sessions/breaks; unique accepted action idempotency key; leave never creates a session.

### WorkdayClassification

Employee+WorkDate classification Scheduled/Leave/Holiday/Absent/Remote/Rest with source reference. Unique per employee/workdate/source priority.

### AttendanceCorrectionRequest

Requested before/after values, reason/evidence, state, approval instance, applied version. Apply once after approval; rejection leaves session unchanged.

## Leave Aggregate

### LeaveType / LeavePolicy / LeavePolicyAssignment

Defines paid ratio, accrual, carry, expiry, attachment, half-day and negative-balance rules, effective-dated assignment.

### LeaveBalance / LeaveLedgerEntry

Balance is cached summary by employee/type/period; ledger is authoritative Credit/Debit/Reserve/Release/Expire/Adjust with source id and uniqueness.

### LeaveRequest

Employee, type, dates/partial-day, computed workdays, reason, attachments, reserved amount, state and approval instance.

Transitions: Draft→Submitted→ManagerApproved→HRApproved/Rejected/Withdrawn/Cancelled. Reserve at submit; convert reserve to debit at final approval; release on rejection/withdrawal.

## Approval Aggregate

### ApprovalDefinition / ApprovalDefinitionStep

Request type, version, SLA, resolver kind and order. Published definitions are immutable.

### ApprovalInstance / ApprovalStepInstance

Snapshot of definition, subject/requester, current state, due date; steps record resolved approver, acting delegate, decision, reason, timestamps and escalation level.

### ApprovalDelegation

Delegator, delegate, scope, start/end, created/revoked by. No overlap to same scope with ambiguous priority. Delegate cannot become self-approver.

Transitions: Pending→Approved/Rejected/Returned/Cancelled/Expired. Each step Pending→Approved/Rejected/Skipped/Escalated; optimistic concurrency prevents double decisions.

## Payroll Aggregate

### PayComponent / PayrollRule / EmployeeCompensation

Component class Earning/Deduction/EmployerContribution/Informational; rule uses constrained expression, eligibility and effective version. Compensation holds effective base/allowance values and never overwrites history.

### PayrollRun

Period, cutoff, status, totals, prepared/reviewed/final-approved/paid/closed actors and timestamps, source data version and reconciliation hash.

Transitions: Draft→Prepared→FinanceReview→FinanceApproved→GMApproved→Paid→Closed; Returned can go to Draft before final approval; Closed immutable.

### EmployeePayroll / PayrollLineItem

Employee snapshot, gross/deductions/net and status. Line item contains component, amount, inputs JSON, explanation, `SourceType`, `SourceId`, `RuleVersionId`, adjustment flag.

Constraints: unique employee/run; unique run+employee+source+component; decimal precision fixed; net derived from lines; no direct edit after GMApproved.

### Payslip / PayrollAdjustment

Payslip immutable version and secured asset reference. Post-close adjustment references original run/line and settles in later run or standalone authorized document.

### Advance / Loan / LoanInstallment / ExpenseClaim / CommissionInput

Durable request, approval, schedule, balance and source linkage. Each paid/deducted installment has a unique payroll source link.

## Documents, Assets & Lifecycle

### EmployeeDocument / EmployeeDocumentVersion

Type, sensitivity, issue/expiry dates, owner, secured asset id, hash, version, verification and archive state.

### Asset / AssetAssignment

Inventory identity, value/status; dated custody records with handover/return condition. Offboarding closure blocked while open custody exists unless exception approval.

### PerformanceCycle / Goal / Review / Appeal

Effective cycle, weighted goals, self/manager scores, signatures, publication and appeal state. Weight total must equal 100% before publish.

### EmployeeCase / CaseEvidence / CaseResponse / DisciplinaryAction

Confidential warning/investigation/penalty, participants, evidence, response right, approval and optional unique payroll impact.

### Requisition / Candidate / Interview / Offer

Recruitment pipeline. Accepted offer provides prefill only; `EmployeeProfileId` appears only after atomic hire succeeds.

### OnboardingPlan / LifecycleTask / OffboardingCase

Task owners, due dates, evidence and blockers for onboarding, probation and exit. Access disablement and final settlement are explicit steps.

## Migration, Audit & Notifications

### HrModuleRollout

Module, state, legacy/new read target, write target, activated/rolled-back actor/time, reconciliation batch. Database constraint permits one write target.

### MigrationBatch / MigrationRecordMap / MigrationConflict

Wave/module/mode DryRun/Final, source checksum, counts/totals, result, old/new identifiers, conflict resolution. Final cutover requires zero unresolved material differences.

### IdempotencyRecord

Scope, actor, key, request hash, response reference and expiry. Same key with different hash is conflict.

### AuditLog

Actor, action, entity, before/after redacted values, reason, correlation, timestamp and scope. Append-only; actor required except named system jobs.

### HrNotification

Event/source/recipient/template/status/dedupe key. Outbox unique key prevents duplicate delivery.

## Delete and retention matrix

| Data | Direct delete | End-of-retention action |
|---|---|---|
| Payroll, payslip, audit, approval decision | Forbidden | Archive according to configured legal policy |
| Assignment, contract, attendance, leave ledger | Forbidden after effective use | Archive or anonymize identity where policy permits |
| Document binary | Allowed only by approved retention job | Delete binary, retain minimal tombstone/hash if required |
| Candidate data | Forbidden while active | Anonymize/archive at configured expiry |
| Draft unused configuration | Soft delete | Purge only if never referenced and audited |
