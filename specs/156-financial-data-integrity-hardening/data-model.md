# Data Model: Financial and Data Integrity Hardening

## StudentBalance

- `CurrentBalance`: decimal, non-negative.
- Relationship: one user has one student balance.
- Validation: `CurrentBalance >= 0`.

## BalanceTransaction

- `Amount`: positive credit or negative debit.
- `BalanceAfter`: resulting balance snapshot, non-negative.
- `TransactionType`: required string.
- `ReferenceId`: optional idempotency reference.
- Validation: filtered unique key on `(TransactionType, ReferenceId)` where `ReferenceId IS NOT NULL`.
- State rule: recharge credits use `TransactionType = DigitalRecharge` and `ReferenceId = RechargeRequest.Id`.

## DigitalWallet

- `CurrentBalance`: observed provider wallet balance, non-negative.
- Validation: `CurrentBalance >= 0`.
- Relationships to recharge requests and incoming SMS logs use restrict/no-action delete behavior.

## RechargeRequest

- State: `Pending -> Matched | Approved | Rejected`.
- `MatchedSmsLogId`: optional one-to-one link to incoming SMS.
- Validation: pending lookup index on `WalletId`, `Status`, `Amount`, `SenderPhoneNumber`, `CreatedAt` for pending rows.
- Transition rule: once non-pending, no later resolution changes state or credits balance.

## IncomingSmsLog

- `IsMatched`: boolean.
- `MatchedRechargeRequestId`: optional link to the request matched by this SMS.
- Validation:
  - `IsMatched = true` iff `MatchedRechargeRequestId IS NOT NULL`.
  - filtered unique key on `MatchedRechargeRequestId` where not null.
  - `DeduplicationHash` remains unique.

## TeacherAccount

- `CurrentBalance`: total payable balance before pending payout reserve, non-negative.
- `ReservedBalance`: amount reserved by pending payouts, non-negative.
- `AvailableBalance`: derived as `CurrentBalance - ReservedBalance`.
- Validation: `CurrentBalance >= 0`, `ReservedBalance >= 0`, `ReservedBalance <= CurrentBalance`.

## TeacherPayout

- State: `Pending -> Paid | Rejected`.
- Request rule: pending payout increments teacher reserved balance.
- Rejection rule: decrements reserved balance only.
- Payment rule: decrements both current and reserved balance.

## StudentAccessGrant

- Target shape by `GrantType`:
  - Package: `PackageId` required.
  - Term: `TermId` required.
  - Month: `ContentSectionId` required.
  - Lesson: `LessonId` required.
  - Video: `LessonVideoId` required.
  - Exam: `ExamId` required.
- Active uniqueness: one active grant per same user, grant type, source reference (`AccessCodeId`, `GiftRecipientId`, `PublicExamProductId`), and target.
- Historical inactive/cancelled rows remain preserved.
