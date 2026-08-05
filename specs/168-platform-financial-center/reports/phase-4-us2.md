# Phase 4 — platform expenses

- Expense lifecycle: draft → posted unpaid/paid → partially paid/paid → reversed.
- Overpayment is rejected before a journal is created.
- Paid expenses debit the configured category and credit the selected treasury; unpaid expenses credit supplier payable.
- Reversal uses the immutable journal reversal path and records the operator reason.
- Admin endpoints: `GET /expenses`, `POST /expenses`, `POST /expenses/{id}/post`, `POST /expenses/{id}/payments`, `POST /expenses/{id}/reverse`.
- Contract coverage: `PlatformExpenseWorkflowTests`, `PlatformExpenseContractTests`, and anonymous Playwright authorization coverage.
