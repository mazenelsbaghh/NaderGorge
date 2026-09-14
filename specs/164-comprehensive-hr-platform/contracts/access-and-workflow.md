# Contract: Access, Workflow and Errors

## Common HTTP rules

- Base path: `/api/hr`؛ legacy `/api/admin/hr` و`/api/hr` الحاليان يبقيان compatibility adapters حتى cutover وحدتهما.
- كل mutation حساس يتطلب `Idempotency-Key`; إعادة نفس body تعيد نفس النتيجة، واختلاف body يعيد `409 IDEMPOTENCY_KEY_REUSED`.
- كل update يحمل `expectedVersion`; التعارض يعيد `409 CONCURRENCY_CONFLICT` مع النسخة الحالية دون values حساسة.
- القوائم تقبل `page`, `pageSize<=100`, `sort`, filters وتعيد `items,total,page,pageSize,scope`.
- الأخطاء: `{ success:false, message, errors:[code], correlationId, fieldErrors? }`.
- الـbackend هو حد الصلاحية؛ route guards مجرد UX.

## Permission families

`hr.employee.read/manage`, `hr.organization.read/manage`, `hr.contract.read/manage`, `hr.shift.read/manage`, `hr.attendance.self/team.read/review/manage`, `hr.leave.self/team.review/hr.review/manage`, `hr.document.self/read/manage`, `hr.asset.self/read/manage`, `hr.performance.self/team/manage`, `hr.case.read/manage`, `hr.recruitment.read/manage`, `hr.report.read/export`, `hr.migration.read/manage`, `payroll.view/configure/prepare/review/final_approve/pay`.

Scopes: `self`, `direct-team`, `organization-subtree`, `all`. Salary, document, case and export permissions never inherit from broad HR access.

## Approval endpoints

- `GET /api/hr/approvals/inbox?requestType=&state=` — pending steps resolved to actor/delegate.
- `POST /api/hr/approvals/{instanceId}/decisions` — `{ decision, reason, expectedVersion }`.
- `GET/POST/PATCH /api/hr/admin/approval-definitions` — versioned configuration.
- `GET/POST/DELETE /api/hr/delegations` — `{ delegateUserId, scope, startsAt, endsAt }`.

Stable failures: `SELF_APPROVAL_FORBIDDEN`, `APPROVER_NOT_ELIGIBLE`, `DELEGATION_INACTIVE`, `STEP_ALREADY_DECIDED`, `APPROVAL_OUT_OF_ORDER`, `REASON_REQUIRED`.

Decision response records original approver, acting user, delegation id, decision time and next step. Scheduler escalation uses `approval-instance/step/escalation-level` as dedupe key.

## Audit contract

Every sensitive mutation emits append-only audit with actor, actor snapshot, action, entity, before/after redacted JSON, reason, IP/request id and correlation id. System actions use a named service actor. Reading/exporting payroll, documents or cases emits access audit without copying content to logs.

## Realtime contract

Outbox events map to existing invalidation scopes: `hr:employees`, `hr:organization`, `hr:shifts`, `hr:attendance`, `hr:leave`, `hr:approvals`, `finance:payroll`, `hr:documents`, `hr:assets`, `hr:performance`, `hr:lifecycle`, `reports`. Payload contains identifiers/version only, never salary/document/case content.
