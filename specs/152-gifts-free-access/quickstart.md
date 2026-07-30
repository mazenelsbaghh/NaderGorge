# Quickstart: Gifts and Free Access

## Prerequisites

- Docker services and external volumes configured for this repository.
- `.env` values required by the existing Compose stack.
- A built-in Admin account, one delegated staff account, at least two active students, two teachers, and purchasable content for each teacher.

## Build and Migrate

```bash
docker compose config -q
make up
make migrate
make ps
```

Open the Admin surface at `http://localhost:8740` and Student surface at `http://localhost:8739`.

## Automated Verification

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj
dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj
(cd frontend && npm run lint)
(cd frontend && npm run build)
(cd frontend && npx playwright test tests/e2e/admin-gifts.spec.ts --project=chromium)
make verify-surfaces
```

## SQL Invariants

Run against PostgreSQL after test scenarios:

```sql
select id
from "promotional_balance_allocations"
where "OriginalAmount" < 0
   or "AvailableAmount" < 0
   or "ConsumedAmount" < 0
   or "ExpiredAmount" < 0
   or "RevokedAmount" < 0
   or "OriginalAmount" <> "AvailableAmount" + "ConsumedAmount" + "ExpiredAmount" + "RevokedAmount";

select "RequestId", count(*)
from "gift_issuances"
group by "RequestId"
having count(*) > 1;

select "GiftIssuanceId", "StudentId", count(*)
from "gift_recipients"
group by "GiftIssuanceId", "StudentId"
having count(*) > 1;

select g.id
from "student_access_grants" g
left join "gift_recipients" r on r.id = g."GiftRecipientId"
where g."GiftRecipientId" is not null and r.id is null;
```

Every query must return zero rows.

## Owner Manual QA

1. As Admin, verify `/admin/gifts` and the direct video-types shell entry are visible.
2. Assign `gifts.manage` to delegated staff; verify gifts become visible but video-types does not.
3. Remove the permission; verify route and API deny access and create no records.
4. Issue package, lesson, video, and exam gifts to multiple students including one invalid/already-entitled recipient; verify partial outcomes.
5. Verify a video-only recipient can play only that video and cannot see sibling content/resources.
6. Verify a video view is counted only after successful session creation and a resumed exam attempt does not consume a second use.
7. Issue general and teacher-restricted promotional balances with expiry/use limits; verify the Student surface distinguishes them from paid balance.
8. Buy eligible and ineligible content; verify earliest-expiry promotional value is used first, then paid balance, with no negative value.
9. Re-submit the same request id and repeat revocation; verify no duplicate grant/value change.
10. Revoke a partially used gift; verify only future/unspent value is removed and prior activity remains.
11. Review ledger, recipient details, and audit evidence for actor, reason, timestamps, and before/after values.

Record status beside every item. Default status is `pending` until the product owner performs it.
