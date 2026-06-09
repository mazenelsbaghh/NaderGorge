# Research & Decision Log: Payroll, Teacher Finance, and Activated Code Accounting

## Summary of Decisions

1. **Entities & Database Schema**:
   - **`PayrollRecord`**: Represents the monthly staff payroll. To support auditability and flexibility, we will also create a **`PayrollAdjustment`** entity. Instead of aggregating all bonuses/deductions into single columns, each addition or deduction will be saved as an individual record with its amount, type (Addition/Deduction), and reason.
   - **`TeacherAccount`**: Tracks teacher earnings. A teacher profile maps 1:1 to a user profile. The `TeacherAccount` will hold the current EGP balance and total earnings, linked directly to the `TeacherProfile` via `TeacherId`.
   - **`TeacherPayout`**: Tracks teacher payout requests with statuses: `Pending`, `Paid`, `Rejected`. Includes auditing fields (`HandledByUserId`, `HandledAt`).
   - **`AccessCodeActivationLog`**: Logs every non-balance access code activation with fields to lock the item's price, commission rate, and calculated commission at the moment of activation.

2. **Access Code Hooking & Transaction Safety**:
   - We will hook the teacher commission calculations directly into the `ActivateCodeCommandHandler` inside its existing serializable transaction block.
   - This ensures atomic changes: the access code is marked as consumed, the student receives the access grant, the `AccessCodeActivationLog` is recorded, and the `TeacherAccount` balance is credited in one database transaction.
   - The original price of the package/term/month/lesson will be retrieved from the DB, discounted if `DiscountPercentage` is set in the `CodeGroup`, and multiplied by the teacher's current `CommissionRate` to compute the commission.

3. **Data Isolation & Role Boundaries**:
   - **Teachers**: Can view only their own `TeacherAccount` balance, transactions (`AccessCodeActivationLog`), and `TeacherPayout` request history. They must be blocked from seeing other teachers' records.
   - **Staff/Assistants**: Assistants are blocked from all administrative finance and payroll endpoints. Only `Admin` and `Supervisor` roles can manage payroll, approve payout requests, and view full financial reports.

4. **Currency Separation**:
   - All balances and adjustments will use `decimal` fields to store real Egyptian Pounds (EGP).
   - EGP calculations are completely decoupled from student gamification points (which are managed in the `StudentGamification` table). No conversion mechanism between points and EGP will be provided.

## Rationale for Decisions

- **Why separate `PayrollAdjustment` table?**: Storing adjustments as separate database records rather than serializing them or using a single text field makes it easy to generate audits, query employee bonuses over time, and display a itemized list of additions and deductions on the admin dashboard.
- **Why Hook in `ActivateCodeCommand`?**: Executing the commission credit directly during code activation prevents reconciliations from getting out of sync. Since `ActivateCodeCommand` already uses `IsolationLevel.Serializable` to handle concurrency and double-activation prevention, reusing this transaction boundary guarantees 100% safety.
- **Why lock CommissionRate and Price?**: Teachers' commission percentages might change in the future (e.g., from 70% to 75%). By copying and locking the price and commission rate inside `AccessCodeActivationLog` at activation time, historical transactions remain intact and accurate.

## Alternatives Considered

- **Alternative 1: Async Event Handler for Commission Crediting**: We could publish an event after the code is activated and handle it asynchronously. While this decouples code activation from finance logic, it introduces eventual consistency risks. If the event handler fails, a student would get access, but the teacher's balance would not be credited. Transactional consistency is preferred here.
- **Alternative 2: Single-field text list for payroll reasons**: Rejected because it prevents structured queries and reports on reasons for employee deductions and bonuses.
