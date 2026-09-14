# Contract: Payroll and Employee Lifecycle

## Payroll configuration

- `GET/POST/PATCH /api/hr/payroll/components` — `payroll.configure`.
- `GET/POST/PATCH /api/hr/payroll/rules` — versioned constrained expressions; publish validates dependencies and cycles.
- `GET/POST /api/hr/payroll/compensation` — effective-dated employee values.

Rule simulation endpoint `POST /api/hr/payroll/rules/simulate` accepts synthetic inputs only and returns lines/explanation without persistence.

## Payroll cycle

- `POST /api/hr/payroll/cycles` — create period, idempotent.
- `POST /api/hr/payroll/cycles/{id}/calculate` — snapshot inputs, `payroll.prepare`.
- `GET /api/hr/payroll/cycles/{id}/employees` — sensitive values require `payroll.view`.
- `POST /api/hr/payroll/cycles/{id}/submit-review`.
- `POST /api/hr/payroll/cycles/{id}/finance-decision` — `payroll.review`.
- `POST /api/hr/payroll/cycles/{id}/final-approval` — `payroll.final_approve`; actor cannot be subject of own exceptional item.
- `POST /api/hr/payroll/cycles/{id}/mark-paid` — `payroll.pay`.
- `POST /api/hr/payroll/settlements` — post-close correction referencing original source.

Calculation result includes gross/deductions/net and each line `{component,amount,sourceType,sourceId,ruleVersion,explanation}`. Recalculation in draft replaces an unpublished calculation atomically; unique source keys prevent double deductions/commissions.

Errors: `PAYROLL_PERIOD_EXISTS`, `PAYROLL_INPUTS_INCOMPLETE`, `PAYROLL_SOURCE_DUPLICATE`, `PAYROLL_INVALID_TRANSITION`, `PAYROLL_CLOSED`, `FINANCE_REVIEW_REQUIRED`, `GM_APPROVAL_REQUIRED`.

## Employee payslip

`GET /api/hr/self/payslips` and `GET /api/hr/self/payslips/{id}/download`. Employee sees only self; every read is audited. Generated document is immutable and versioned.

## Loans, advances and expenses

Self requests under `/api/hr/self/financial-requests`; administration under `/api/hr/payroll/financial-requests`. Installment schedule and balance are returned. Approval follows configured workflow; payroll source link is unique per installment/claim/commission.

## Documents and assets

- Self: `/api/hr/self/documents`, `/api/hr/self/assets`.
- Admin: `/api/hr/admin/documents`, `/api/hr/admin/assets`.
- Download returns short-lived authorized stream, never a public URL.

## Performance, cases and lifecycle

Admin/manager endpoints under `/api/hr/admin/performance`, `/api/hr/admin/cases`, `/api/hr/admin/recruitment`, `/api/hr/admin/lifecycle`. Case existence and evidence are hidden without case permission. Accepted candidate calls employee provisioning once; it does not create a partial profile. Offboarding close returns blockers for assets, access tasks and settlement.
