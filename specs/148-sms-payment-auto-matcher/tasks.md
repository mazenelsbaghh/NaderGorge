# Tasks: SMS Payment Auto-Matcher

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic database structures

- [x] T001 Create `RechargeRequestStatus` enum in `backend/src/NaderGorge.Domain/Enums/RechargeRequestStatus.cs`
- [x] T002 Create `DigitalWallet` entity in `backend/src/NaderGorge.Domain/Entities/DigitalWallet.cs`
- [x] T003 Create `RechargeRequest` entity in `backend/src/NaderGorge.Domain/Entities/RechargeRequest.cs`
- [x] T004 Create `IncomingSmsLog` entity in `backend/src/NaderGorge.Domain/Entities/IncomingSmsLog.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Database context configuration, migrations, and shared service updates

- [x] T005 Register `DigitalWallets`, `RechargeRequests`, and `IncomingSmsLogs` DbSets and configure their entity relationships (cascade behavior, unique indexes on PhoneNumber/PairingToken/DeduplicationHash, decimal precision) in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [x] T006 Add EF Core database migration for the new wallet and payment tables using `dotnet ef migrations add AddSmsPaymentAutoMatcher`
- [x] T007 Apply database migrations to the database using `dotnet ef database update`
- [x] T008 Update `BalanceService` in `backend/src/NaderGorge.Application/Services/BalanceService.cs` to allow custom transaction type parameter in `AddCredit` method to support `"DigitalRecharge"`

---

## Phase 3: User Story 1 - Admin Creates a Digital Wallet & Generates Pairing Code (Priority: P1)

**Goal**: Allow admins to add wallets, configure limits, and retrieve pairing tokens.

**Independent Test**: Verify that calling the create wallet endpoint successfully returns a new wallet with an 8-character pairing token and status `Disconnected`.

### Implementation for User Story 1
- [x] T009 Create wallet request/response DTOs in `backend/src/NaderGorge.Application/Features/Admin/Wallets/`
- [x] T010 Implement the create/list wallet query and command handlers using MediatR in `backend/src/NaderGorge.Application/Features/Admin/Wallets/`
- [x] T011 Create Admin Wallets API endpoint in `backend/src/NaderGorge.API/Controllers/AdminWalletsController.cs`
- [x] T012 Implement Admin Wallet Management page in Next.js web application under `frontend/src/app/admin/wallets/page.tsx`

---

## Phase 4: User Story 2 - Android App Pairs with Server Using Pairing Code (Priority: P1)

**Goal**: Permit Android devices to authenticate with the server using only the pairing token.

**Independent Test**: Test with a pairing token to verify that the sync API returns 200 OK and transitions wallet status to `Connected`.

### Implementation for User Story 2
- [x] T013 Create the `POST /api/android/sync-status` sync endpoint and request/response models in `backend/src/NaderGorge.API/Controllers/AndroidWalletController.cs`
- [x] T014 Implement pairing logic in the controller to validate `X-Pairing-Token`, update `DeviceStatus` to `Connected`, and set `LastSeenAt = DateTime.UtcNow`
- [x] T015 Scaffold the Kotlin Android app in `mobile/payment-listener-android` with necessary Gradle dependencies, AndroidManifest permissions (SMS and network), and API client settings
- [x] T016 Build Setup Screen UI in Jetpack Compose to input Server URL and Pairing Code, saving configuration securely in SharedPreferences

---

## Phase 5: User Story 3 - Android App Captures and Forwards SMS (Priority: P1)

**Goal**: Background capturing and forwarding of digital wallet SMS.

**Independent Test**: Simulate an SMS from `VodafoneCash` and verify it is captured and saved in the database's `IncomingSmsLog` table.

### Implementation for User Story 3
- [x] T017 Implement `POST /api/android/sms` endpoint in `backend/src/NaderGorge.API/Controllers/AndroidWalletController.cs` to receive and deduplicate SMS payloads
- [x] T018 Build `SmsReceiver` class in Android project to listen for SMS broadcasts and filter by configured sender list
- [x] T019 Implement WorkManager sync task `SmsSyncWorker` in Android project to queue captures locally when offline and upload to server on network restore

---

## Phase 6: User Story 4 - Student Initiates Recharge Request (Priority: P1)

**Goal**: Students can request a balance recharge, get assigned a wallet, and upload transaction proof.

**Independent Test**: Initiate recharge and verify that a wallet is reserved for 20 minutes, and submission transitions state to `Pending`.

### Implementation for User Story 4
- [x] T020 Create student recharge request/response DTOs and command handlers in `backend/src/NaderGorge.Application/Features/Student/Recharge/`
- [x] T021 Implement wallet assignment logic that selects the active wallet with the highest remaining daily/monthly capacity
- [x] T022 Create Student Recharge API endpoints `POST /api/student/recharge/initiate` and `POST /api/student/recharge/submit` (with screenshot file upload) in `backend/src/NaderGorge.API/Controllers/StudentRechargeController.cs`
- [x] T023 Add background task or logic in EF Core query to expire pending requests and release wallet capacity after 20 minutes
- [x] T024 Implement Student Recharge layout and form in Next.js web application under `frontend/src/app/student/recharge/page.tsx`

---

## Phase 7: User Story 5 - Server Auto-Matches SMS with Recharge Request (Priority: P1)

**Goal**: Automatic matching of incoming SMS details with pending student recharge requests.

**Independent Test**: Create a pending request and post a matching SMS, verifying that the student's balance is automatically credited.

### Implementation for User Story 5
- [x] T025 Implement C# regex parser logic on the server to extract transfer amount and sender phone number from SMS bodies
- [x] T026 Create auto-matching transaction pipeline that compares SMS details to pending requests under a 2-hour window, credits student balance, and marks request as `Matched`
- [x] T027 Add deduplication hashing logic to verify message uniqueness before executing matches

---

## Phase 8: User Story 6 - Admin Reviews Unmatched Transactions (Priority: P2)

**Goal**: Allow manual matching and resolution of unmatched requests and SMS logs.

**Independent Test**: Review verification queue, click approve/reject, and confirm appropriate balance updates and status transitions.

### Implementation for User Story 6
- [ ] T028 Create admin resolve/verification queries and command handlers in `backend/src/NaderGorge.Application/Features/Admin/Recharge/`
- [ ] T029 Implement verification API endpoints in `backend/src/NaderGorge.API/Controllers/AdminWalletsController.cs`
- [ ] T030 Create Admin Pending Verification Queue UI in Next.js web application under `frontend/src/app/admin/recharge-verification/page.tsx`

---

## Phase 9: User Story 7 & 8 - Android App Dashboard & Admin Operations (Priority: P2)

**Goal**: Wallet overview inside the app and admin management controls on the web.

**Independent Test**: Verify other wallets load on Android screen, and deactivating a wallet removes it from allocation.

### Implementation for User Stories 7 & 8
- [ ] T031 Implement Kotlin UI Dashboard showing wallet phone, balance, and other active devices list
- [ ] T032 Add heartbeat updater to Android app to periodically sync status and reload configuration
- [ ] T033 Implement admin toggle controls (activate/deactivate wallet, edit limits) in web admin panel and API controller

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Code cleanup, refactoring, and quality gates

- [ ] T034 Code cleanup, styling fixes, and API optimization
- [ ] T035 Write unit tests for SMS parser logic in `tests/NaderGorge.UnitTests/Services/SmsParserTests.cs`
- [ ] T036 Run `clean-code-guard` against all modified backend and frontend files
- [ ] T037 Run `test-guard` on test files
- [ ] T038 Validate full Docker setup and verify backend/frontend compilation
