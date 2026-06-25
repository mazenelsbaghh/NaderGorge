# Research: SMS Payment Auto-Matcher

**Date**: 2026-06-25
**Feature**: 148-sms-payment-auto-matcher

## Decision 1: Android SMS Receiver Strategy

**Decision**: Use `BroadcastReceiver` registered in `AndroidManifest.xml` with `SMS_RECEIVED` intent + `WorkManager` for reliable delivery to server.

**Rationale**: Manifest-declared BroadcastReceiver is awakened by the OS even when the app process is killed, ensuring no SMS is missed. WorkManager with `NetworkType.CONNECTED` constraint guarantees delivery even after offline periods. This matches the existing `parent-android` app's architecture (Kotlin + Jetpack Compose + Retrofit).

**Alternatives considered**:
- `SmsRetriever API`: Only works for app-specific OTPs with hash, not for general wallet SMS. Rejected.
- `ContentObserver` on SMS inbox: Requires `READ_SMS` and continuous polling. Less efficient and battery-friendly. Rejected.
- `NotificationListenerService`: Only for push notifications, not SMS. Out of scope per user decision.

## Decision 2: Authentication for Android ↔ Server Communication

**Decision**: Use pairing token (8-character alphanumeric code) sent as `X-Pairing-Token` HTTP header on all Android API requests. No JWT, no username/password.

**Rationale**: User explicitly requested no login screen. The pairing token is generated server-side, stored in `EncryptedSharedPreferences` on the device, and validated on every request. This keeps the app simple and the security model tight (tokens are per-wallet, revocable by the admin by regenerating).

**Alternatives considered**:
- JWT with auto-refresh: Adds complexity without benefit since there's no user identity to track. Rejected.
- API key in environment: Less flexible (can't revoke per-device). Rejected.

## Decision 3: SMS Parsing Strategy

**Decision**: Server-side parsing using C# Regex patterns. The Android app sends raw SMS body; the server extracts structured data.

**Rationale**: Centralizing parsing logic on the server means updates to regex patterns (when Vodafone Cash changes their SMS format) don't require an Android app update. The server already has good regex support in C#.

**Patterns to handle (Vodafone Cash Egypt example)**:
```
"تم استلام {amount} جنيه من {phone_number}. الرصيد الحالي {balance} جنيه."
"You received {amount} EGP from {phone_number}. Current balance {balance} EGP."
```

**Alternatives considered**:
- Client-side (Android) parsing: Would require APK updates for format changes. Rejected.
- AI/ML-based extraction: Overkill for structured SMS; regex is deterministic and fast. Rejected.

## Decision 4: Wallet Assignment Strategy for Students

**Decision**: Least-loaded wallet selection with temporary reservation. Server picks the active wallet with the most remaining daily capacity, reserves the specified amount for 20 minutes, and returns the wallet phone number to the student.

**Rationale**: Distributes load across wallets evenly. The reservation prevents over-allocation of daily limits. The 20-minute timeout frees unrealized reservations.

**Alternatives considered**:
- Round-robin: Simpler but ignores capacity differences. Rejected.
- Student picks the wallet: Exposes internal wallet management to students. Rejected.
- No reservation (first-come-first-served): Risk of exceeding limits if multiple students target the same wallet simultaneously. Rejected.

## Decision 5: File Upload for Screenshots

**Decision**: Reuse existing file upload pattern from `AdminController.UploadResourceFile` — save to `wwwroot/uploads/recharge-screenshots/` with GUID-prefixed filename. Accept image MIME types only. Max 5MB.

**Rationale**: Consistent with existing codebase patterns. Uses `IFormFile`, saves to `wwwroot`, returns relative URL path.

**Alternatives considered**:
- S3/cloud storage: Not currently used in the project. Would add infrastructure complexity. Rejected.
- Base64 inline: Bloats API request, no advantage. Rejected.

## Decision 6: Android Dashboard Data Sync

**Decision**: Polling via `POST /api/android/sync-status` every 30 seconds. Returns all wallets' statuses, balances, and the latest SMS sender filter list.

**Rationale**: Simple, reliable, and sufficient for the ~3 wallets expected. SignalR is used in the project but would be overkill for this low-frequency, low-count dashboard. The sync endpoint also serves as the heartbeat for connection status tracking.

**Alternatives considered**:
- SignalR/WebSocket: More complex, more battery drain on Android. Not justified for 3-5 devices. Deferred to future if needed.
- Firebase Cloud Messaging push: Adds Firebase dependency for data sync (FCM is already in parent app but for notifications, not data sync). Rejected.

## Decision 7: Duplicate SMS Detection

**Decision**: Hash-based deduplication. Generate SHA256 hash of `walletId + sender + body + receivedAt(rounded to minute)`. Check for existing hash before processing.

**Rationale**: SMS can be delivered multiple times by the carrier. Hashing the key fields prevents double-crediting while allowing legitimate repeated transfers of the same amount.

**Alternatives considered**:
- Exact body match: Fragile if timestamp or balance in SMS body changes slightly. Rejected.
- Transaction ID extraction: Not all SMS formats include a transaction ID. Unreliable as primary key. May be used as supplementary match if present.

## Decision 8: Existing Infrastructure Reuse

**Decision**: Reuse `StudentBalance` and `BalanceTransaction` entities for crediting. Add a new `TransactionType = "DigitalRecharge"`.

**Rationale**: The existing balance system (`StudentBalance.CurrentBalance`, `BalanceTransaction` with `Amount`, `BalanceAfter`, `TransactionType`, `Description`) is a perfect fit. The `PurchaseContentCommand` shows the exact pattern for debits; we just need the inverse (credit).

**Evidence**: `StudentBalance.cs` has `CurrentBalance` and `Transactions` collection. `BalanceTransaction` has `TransactionType` as a string enum with values like `CodeRedemption`, `ContentPurchase`, `AdminAdjustment`. We add `DigitalRecharge`.
