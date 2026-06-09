# Research & Analysis: Audit Trail and Reports Cockpit

## Subsystem Write Auditing

We identified that while some commands in HR and Payroll already write to `AuditLogs`, several key operational commands are missing audit logs. We will modify the following commands to inject `IAuditRepository` or write directly to `_db.AuditLogs`:

### 1. Operations Task Manager
* **`CreateTaskCommand`**: Log action `"CreateTask"`, entity `"TaskItem"`.
* **`UpdateTaskStatusCommand`**: Log action `"UpdateTaskStatus"`, entity `"TaskItem"`, logging previous and updated status.
* **`AdminResolveApprovalCommand`**: Log action `"ResolveTaskApproval"`, entity `"TaskItem"`.

### 2. CRM (Call Center)
* **`LogCrmCallCommand`**: Log action `"LogCrmCall"`, entity `"CrmCallLog"`.
* **`AssignStudentToAgentCommand`**: Log action `"AssignStudentToCrmAgent"`, entity `"CrmStudentStatus"`.

### 3. Media Production
* **`CreateMediaPipelineCommand`**: Log action `"CreateMediaPipeline"`, entity `"MediaProductionPipeline"`.
* **`UpdateMediaPipelineCommand`**: Log action `"UpdateMediaPipeline"`, entity `"MediaProductionPipeline"`.
* **`CreateSocialPlanCommand"**: Log action `"CreateSocialMediaPlan"`, entity `"SocialMediaPlan"`.

### 4. Payments & Code Activations
* **`ActivateCodeCommand`**: Log action `"ActivateCode"`, entity `"AccessCode"`.

---

## KPI Dashboard Calculations

The reports dashboard will query aggregates from the database. To optimize performance and ensure accuracy, the queries will calculate:

### 1. Attendance Metrics
* **Total logs**: Count of `AttendanceLog` where Date falls in date range.
* **Present Count**: `Status == Present`
* **Late Count**: `Status == Late`
* **Absent Count**: `Status == Absent`
* **Rates**: Calculate percentages (e.g. `(PresentCount / Total) * 100`).

### 2. Task Completion Metrics
* **Total Tasks**: Count of `TaskItem` created or due in range.
* **Completed Count**: `Status == Completed`
* **Overdue Count**: `Status != Completed && DueDate < UtcNow`
* **Pending Count**: `Status != Completed && DueDate >= UtcNow`

### 3. CRM Outcome Distribution
* Group `CrmCallLog` by `Outcome` (e.g., Interested, Busy, NoAnswer, Answered) and return counts.

### 4. Media Production Delays
* Select completed pipelines (`Stage == Published`).
* Average difference in days between `CreatedAt` and the timestamp of when it was published.

### 5. Payment Matching Rate
* Auto-Matched (Balance purchases) vs Coupon-Activated (Access codes).
* Calculated as: `(Count of BalanceTransactions / Total activations & purchases) * 100`.

### 6. Payroll Approval Status
* Count of `PayrollRecord` grouped by `Status` (Draft vs Approved) in range.

---

## Sensitive Fields Redaction Mechanism

To comply with privacy and security:
* We will write a utility helper `AuditSanitizer.Sanitize(object payload)` or a custom JSON serializer.
* It will search for keys like `Password`, `PasswordHash`, `Token`, `ClientSecret`, `VerificationCode`, `AccessCode` and replace their values with `"[REDACTED]"`.
