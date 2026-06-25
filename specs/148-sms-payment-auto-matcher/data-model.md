# Data Model: SMS Payment Auto-Matcher

**Date**: 2026-06-25
**Feature**: 148-sms-payment-auto-matcher

## New Entities

### DigitalWallet

Represents a mobile money wallet connected to the system via an Android device.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | Guid | PK, auto-generated | Inherited from BaseEntity |
| PhoneNumber | string | Required, Unique, Max 20 | Egyptian phone format "01XXXXXXXXX" |
| Label | string | Required, Max 100 | Admin-defined label, e.g. "محفظة فودافون كاش 1" |
| DailyLimit | decimal | Required, > 0 | Max EGP receivable per calendar day |
| MonthlyLimit | decimal | Required, > 0 | Max EGP receivable per calendar month |
| CurrentBalance | decimal | Default 0 | Running balance tracked by the system |
| PairingToken | string | Unique, 8 chars alphanumeric | Generated on creation, regeneratable |
| DeviceStatus | string | "Connected" or "Disconnected" | Updated by heartbeat sync |
| LastSeenAt | DateTime? | Nullable | Timestamp of last heartbeat from device |
| IsActive | bool | Default true | Admin toggle to enable/disable for student use |
| SmsSenderFilters | string | JSON array, e.g. ["VodafoneCash","VF-Cash"] | Configurable SMS sender names to listen for |
| CreatedAt | DateTime | Auto | Inherited from BaseEntity |
| UpdatedAt | DateTime? | Auto | Inherited from BaseEntity |

**Relationships**:
- One-to-Many → `RechargeRequest` (one wallet has many requests)
- One-to-Many → `IncomingSmsLog` (one wallet has many SMS logs)

**State transitions**: Active ↔ Inactive (admin toggle). Connected ↔ Disconnected (heartbeat-based, auto-disconnect after 2 minutes without heartbeat).

---

### RechargeRequest

Represents a student's attempt to recharge their balance via digital transfer.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | Guid | PK, auto-generated | Inherited from BaseEntity |
| StudentId | Guid | FK → User.Id, Required | The student requesting the recharge |
| WalletId | Guid | FK → DigitalWallet.Id, Required | The wallet assigned for this transfer |
| Amount | decimal | Required, > 0 | Amount the student claims to have transferred |
| SenderPhoneNumber | string | Required, Max 20 | Phone number student transferred from |
| ScreenshotUrl | string? | Nullable | URL to uploaded screenshot image |
| Status | RechargeRequestStatus enum | Required | Pending, Matched, Approved, Rejected, Expired |
| ResolvedAt | DateTime? | Nullable | When the request was matched/approved/rejected |
| ResolvedByUserId | Guid? | FK → User.Id, Nullable | Admin who manually approved/rejected |
| RejectionReason | string? | Nullable, Max 500 | Reason if rejected |
| MatchedSmsLogId | Guid? | FK → IncomingSmsLog.Id, Nullable | The SMS that was auto-matched |
| ReservationExpiresAt | DateTime? | Nullable | When the 20-minute wallet reservation expires |
| CreatedAt | DateTime | Auto | Inherited from BaseEntity |
| UpdatedAt | DateTime? | Auto | Inherited from BaseEntity |

**Status lifecycle**:
```
Pending → Matched (auto-match success)
Pending → Approved (manual admin approval)
Pending → Rejected (manual admin rejection)
Pending → Expired (20-min reservation timeout without submission, or unmatched after extended period)
```

**Relationships**:
- Many-to-One → `User` (student)
- Many-to-One → `DigitalWallet`
- One-to-One → `IncomingSmsLog` (optional, when matched)

---

### IncomingSmsLog

Represents an SMS message captured by an Android device and forwarded to the server.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | Guid | PK, auto-generated | Inherited from BaseEntity |
| WalletId | Guid | FK → DigitalWallet.Id, Required | The wallet/device that received this SMS |
| Sender | string | Required, Max 100 | SMS sender name (e.g. "VodafoneCash") |
| Body | string | Required, Max 1000 | Full SMS text |
| ReceivedAt | DateTime | Required | When the SMS was received on the device |
| ParsedAmount | decimal? | Nullable | Extracted amount from SMS body (null if parse fails) |
| ParsedSenderPhone | string? | Nullable, Max 20 | Extracted phone number from SMS body |
| IsMatched | bool | Default false | Whether this SMS was matched to a RechargeRequest |
| MatchedRechargeRequestId | Guid? | FK → RechargeRequest.Id, Nullable | The matched request |
| DeduplicationHash | string | Required, Unique, Max 64 | SHA256 of walletId+sender+body+receivedAt(minute) |
| CreatedAt | DateTime | Auto | Inherited from BaseEntity |

**Relationships**:
- Many-to-One → `DigitalWallet`
- One-to-One → `RechargeRequest` (optional, when matched)

---

## Modified Entities

### BalanceTransaction (existing)

Add new `TransactionType` value: `"DigitalRecharge"` for balance credits from successful recharge matching. The `ReferenceId` field will store the `RechargeRequest.Id`.

---

## Enums

### RechargeRequestStatus

```csharp
public enum RechargeRequestStatus
{
    Pending = 0,
    Matched = 1,    // Auto-matched with incoming SMS
    Approved = 2,   // Manually approved by admin
    Rejected = 3,   // Manually rejected by admin
    Expired = 4     // Reservation expired or request timed out
}
```

## Indexes

| Table | Index | Type | Purpose |
|-------|-------|------|---------|
| DigitalWallet | PhoneNumber | Unique | Prevent duplicate wallets |
| DigitalWallet | PairingToken | Unique | Fast token lookup for Android auth |
| RechargeRequest | StudentId, Status | Composite | Fast lookup of student's pending requests |
| RechargeRequest | WalletId, Status, CreatedAt | Composite | Fast matching against incoming SMS |
| IncomingSmsLog | DeduplicationHash | Unique | Prevent duplicate SMS processing |
| IncomingSmsLog | WalletId, IsMatched | Composite | Admin verification queue query |
