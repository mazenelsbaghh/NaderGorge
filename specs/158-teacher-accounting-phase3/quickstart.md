# Quickstart: Teacher Accounting Phase 3

## Preconditions

- Phase 1 sales/content and Phase 2 financial integrity migrations are present.
- Admin, teacher, and student test users exist.
- At least one single-teacher lesson/package/public exam exists.
- At least two teacher profiles exist for shared-package testing.

## Phase 3 Implementation Notes

- Teacher ledger writes now use `TeacherAccountingService` with idempotency keys for code activation, direct purchases, and shared package purchases.
- Teacher finance reads are ledger-backed and include today earnings, available/reserved/debt balances, calendar summaries, and transaction details.
- Payout lifecycle is split into `Pending` → `Approved` (ready for transfer) → `Paid` (actual transfer recorded); rejection releases reserved balance.
- Admin finance has a teacher-event review tab and API endpoints for pending/approved/rejected allocations.
- Shared packages are available through `/admin/shared-packages` and `/student/shared-packages`; the backend validates over-allocation and records teacher/platform shares.
- Free or 100% discounted operations remain zero-value tracking unless an admin creates explicit manual compensation through the finance API.
- Public teacher endpoints expose profile, subjects, packages, shared packages, lessons, and teacher-scoped community posts while preserving the existing student teacher list DTO shape.
- Some planned application-layer handler/test split tasks remain future cleanup because this implementation kept several small flows in API controllers to match the current codebase pattern and finish the usable path.

## Automated Verification

1. Build and run focused backend tests:

   ```bash
   dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~TeacherAccountingPhase3Tests|FullyQualifiedName~SharedPackageAccountingTests|FullyQualifiedName~CommissionTests|FullyQualifiedName~FinancialDataIntegrityTests|FullyQualifiedName~PublicTeacherProfileTests"
   ```

2. Build backend API:

   ```bash
   dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj
   ```

3. Build/lint frontend:

   ```bash
   cd frontend && npm run lint && npm run build
   ```

4. Validate Docker configuration:

   ```bash
   docker compose config -q
   ```

## Manual QA Flow

1. Sign in as admin.
2. Create or verify a teacher profile with public profile enabled.
3. Create a single-teacher paid lesson/package/public exam.
4. Create a shared package with at least two teachers:
   - one teacher with percentage allocation,
   - one teacher with fixed amount allocation.
5. Sign in as student and buy:
   - a single-teacher content item,
   - a public exam,
   - a shared package,
   - a free or 100% discounted item.
6. Sign in as each teacher and open `/teacher/finance`:
   - verify today's income,
   - verify calendar bucket,
   - open the transaction day,
   - confirm only that teacher's allocations appear.
7. Sign in as admin and open the teacher finance review:
   - review pending/suspicious events,
   - approve valid events,
   - reject invalid events with a reason.
8. Teacher requests payout.
9. Admin approves payout so it becomes ready for transfer.
10. Admin records the real transfer by marking payout paid with a transfer reference.
11. Verify balances:
    - pending approval reserves balance,
    - approval keeps reserve held,
    - paid deducts current/reserved balance,
    - rejection releases reserve.
12. Trigger or simulate a refund/cancel:
    - before payout: unpaid earning reverses,
    - after payout: negative adjustment/debt appears for next cycle.
13. Open public teacher profile as student before and after purchase:
    - profile and browse-safe content appear,
    - access-aware content state changes after purchase,
    - teacher-scoped community posts/comments remain moderated.

## Expected Roadmap Closure

After automated and manual verification, update `docs/platform-change-roadmap.md` Phase 3 checkboxes with short completion evidence. Leave any unverified manual item unchecked with a blocker note.
