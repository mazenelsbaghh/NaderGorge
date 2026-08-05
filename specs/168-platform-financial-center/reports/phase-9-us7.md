# Phase 9 — reports and period close

- Screen reports, XLSX, and PDF use bounded posted-journal rows for the requested period.
- Profit/loss, cash flow, financial position, expenses, and refunds are available as filtered report datasets.
- Period close/reopen requires a reason, writes an audit log, and the posting engine rejects closed-period mutations.
- Export and close contracts are covered by integration tests and anonymous endpoint authorization tests.
