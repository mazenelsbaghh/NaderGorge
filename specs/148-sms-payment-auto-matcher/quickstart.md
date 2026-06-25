# Quickstart: SMS Payment Auto-Matcher

**Date**: 2026-06-25
**Feature**: 148-sms-payment-auto-matcher

## Prerequisites

- .NET 9 SDK
- Node.js 20+
- PostgreSQL 16 (or Docker)
- Android Studio (for Android app development)
- An Android device or emulator with SMS capability

## Backend Setup

```bash
# From repo root
cd backend

# Run migrations (creates new tables: DigitalWallets, RechargeRequests, IncomingSmsLogs)
dotnet ef database update --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API

# Run backend
dotnet run --project src/NaderGorge.API
```

## Frontend Setup

```bash
# From repo root
cd frontend
npm install
npm run dev
```

## Android App Setup

```bash
# From repo root
cd mobile/payment-listener-android

# Open in Android Studio or build from command line
./gradlew assembleDebug

# Install on device/emulator
adb install app/build/outputs/apk/debug/app-debug.apk
```

## Testing the Full Flow

### 1. Create a wallet (Admin)
```bash
curl -X POST http://localhost:5245/api/admin/wallets \
  -H "Authorization: Bearer <admin-jwt>" \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"01012345678","label":"Test Wallet","dailyLimit":30000,"monthlyLimit":100000,"smsSenderFilters":["VodafoneCash"]}'
```

### 2. Pair the Android app
- Copy the `pairingCode` from the response
- Open the Android app
- Enter server URL: `http://<server-ip>:5245`
- Enter pairing code
- Dashboard should load with wallet info

### 3. Initiate a recharge (Student)
```bash
curl -X POST http://localhost:5245/api/student/recharge/initiate \
  -H "Authorization: Bearer <student-jwt>" \
  -H "Content-Type: application/json" \
  -d '{"amount":100}'
```

### 4. Submit recharge proof (Student)
```bash
curl -X POST http://localhost:5245/api/student/recharge/submit \
  -H "Authorization: Bearer <student-jwt>" \
  -F "rechargeRequestId=<id-from-step-3>" \
  -F "senderPhoneNumber=01098765432" \
  -F "amount=100" \
  -F "screenshot=@/path/to/screenshot.jpg"
```

### 5. Simulate SMS (for testing)
```bash
# Send a test SMS to the server as if the Android app captured it
curl -X POST http://localhost:5245/api/android/sms \
  -H "X-Pairing-Token: <pairing-token>" \
  -H "Content-Type: application/json" \
  -d '{"sender":"VodafoneCash","body":"تم استلام 100.00 جنيه من 01098765432. الرصيد الحالي 15100.00 جنيه.","receivedAt":"2026-06-25T15:35:00Z"}'
```

### 6. Verify auto-match
- Check student balance was credited
- Check recharge request status is "Matched"

## Docker

```bash
# Full stack with Docker
make up
make migrate
# Verify services
make ps
```

## Key URLs

| Service | URL |
|---------|-----|
| Backend API | http://localhost:5245 |
| Swagger | http://localhost:5245/swagger |
| Frontend | http://localhost:8738 |
| Admin Wallets | http://localhost:8738/admin/wallets |
| Student Recharge | http://localhost:8738/student/balance (new recharge button) |
