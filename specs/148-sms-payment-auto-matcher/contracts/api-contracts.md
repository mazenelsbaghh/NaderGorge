# API Contracts: SMS Payment Auto-Matcher

**Date**: 2026-06-25
**Feature**: 148-sms-payment-auto-matcher

---

## Android Device API (`/api/android/`)

Authentication: `X-Pairing-Token` header required on all requests.

### POST /api/android/pair

Validates a pairing code and returns the wallet configuration.

**Request**: `application/json`
```json
{
  "pairingCode": "AB12CD34"
}
```

**Response 200**: `application/json`
```json
{
  "success": true,
  "data": {
    "walletId": "guid",
    "phoneNumber": "01012345678",
    "label": "محفظة فودافون كاش 1",
    "pairingToken": "full-token-for-subsequent-requests",
    "smsSenderFilters": ["VodafoneCash", "VF-Cash"]
  }
}
```

**Response 400**: Invalid or expired pairing code.

---

### POST /api/android/sync-status

Heartbeat + sync. Updates device status and returns all wallets' info.

**Request**: `application/json`
```json
{
  "currentBalance": 15000.00
}
```

**Response 200**: `application/json`
```json
{
  "success": true,
  "data": {
    "thisWallet": {
      "id": "guid",
      "phoneNumber": "01012345678",
      "label": "محفظة فودافون كاش 1",
      "currentBalance": 15000.00,
      "dailyLimit": 30000.00,
      "monthlyLimit": 100000.00,
      "receivedToday": 5000.00,
      "receivedThisMonth": 45000.00,
      "isActive": true
    },
    "otherWallets": [
      {
        "id": "guid",
        "phoneNumber": "01098765432",
        "label": "محفظة اتصالات كاش",
        "currentBalance": 22000.00,
        "dailyLimit": 30000.00,
        "monthlyLimit": 100000.00,
        "receivedToday": 8000.00,
        "receivedThisMonth": 60000.00,
        "deviceStatus": "Connected",
        "lastSeenAt": "2026-06-25T15:30:00Z",
        "isActive": true
      }
    ],
    "smsSenderFilters": ["VodafoneCash", "VF-Cash"],
    "recentSmsCount": 12
  }
}
```

---

### POST /api/android/sms

Submit a captured SMS message for processing.

**Request**: `application/json`
```json
{
  "sender": "VodafoneCash",
  "body": "تم استلام 100.00 جنيه من 01098765432. الرصيد الحالي 15100.00 جنيه.",
  "receivedAt": "2026-06-25T15:35:00Z"
}
```

**Response 200**: `application/json`
```json
{
  "success": true,
  "data": {
    "smsLogId": "guid",
    "isMatched": true,
    "matchedStudentName": "أحمد محمد",
    "matchedAmount": 100.00
  }
}
```

**Response 200 (unmatched)**: `application/json`
```json
{
  "success": true,
  "data": {
    "smsLogId": "guid",
    "isMatched": false,
    "matchedStudentName": null,
    "matchedAmount": null
  }
}
```

**Response 409**: Duplicate SMS (already processed).

---

## Student API (`/api/student/recharge/`)

Authentication: Standard JWT Bearer token.

### POST /api/student/recharge/initiate

Start a recharge flow. Server picks a wallet and reserves capacity.

**Request**: `application/json`
```json
{
  "amount": 100.00
}
```

**Response 200**: `application/json`
```json
{
  "success": true,
  "data": {
    "rechargeRequestId": "guid",
    "walletPhoneNumber": "01012345678",
    "amount": 100.00,
    "expiresAt": "2026-06-25T16:05:00Z"
  }
}
```

**Response 400**: No available wallets (all at limit).
```json
{
  "success": false,
  "message": "عذراً، خدمة الشحن غير متاحة حالياً. حاول مرة أخرى لاحقاً."
}
```

---

### POST /api/student/recharge/submit

Submit transfer proof after completing the transfer.

**Request**: `multipart/form-data`
| Field | Type | Required |
|-------|------|----------|
| rechargeRequestId | string (GUID) | Yes |
| senderPhoneNumber | string | Yes |
| amount | decimal | Yes |
| screenshot | file (image/*) | Yes |

**Response 200**: `application/json`
```json
{
  "success": true,
  "data": {
    "rechargeRequestId": "guid",
    "status": "Pending",
    "message": "تم تسجيل طلب الشحن بنجاح. سيتم تأكيد الرصيد فور التحقق."
  }
}
```

**Response 400**: Invalid request ID, expired reservation, or missing fields.

---

### GET /api/student/recharge/status/{id}

Check the status of a recharge request.

**Response 200**: `application/json`
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "amount": 100.00,
    "status": "Matched",
    "createdAt": "2026-06-25T15:40:00Z",
    "resolvedAt": "2026-06-25T15:41:30Z"
  }
}
```

---

## Admin API (`/api/admin/wallets/` & `/api/admin/recharge/`)

Authentication: JWT Bearer + `payments.manage` permission.

### GET /api/admin/wallets

List all wallets with stats.

**Response 200**: `application/json`
```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "phoneNumber": "01012345678",
      "label": "محفظة فودافون كاش 1",
      "dailyLimit": 30000.00,
      "monthlyLimit": 100000.00,
      "currentBalance": 15000.00,
      "receivedToday": 5000.00,
      "receivedThisMonth": 45000.00,
      "deviceStatus": "Connected",
      "lastSeenAt": "2026-06-25T15:30:00Z",
      "isActive": true,
      "pairingCode": "AB12CD34",
      "smsSenderFilters": ["VodafoneCash", "VF-Cash"]
    }
  ]
}
```

### POST /api/admin/wallets

Create a new wallet.

**Request**: `application/json`
```json
{
  "phoneNumber": "01012345678",
  "label": "محفظة فودافون كاش 1",
  "dailyLimit": 30000.00,
  "monthlyLimit": 100000.00,
  "smsSenderFilters": ["VodafoneCash", "VF-Cash"]
}
```

### PUT /api/admin/wallets/{id}

Update wallet settings.

### POST /api/admin/wallets/{id}/regenerate-pairing

Regenerate pairing code (disconnects current device).

### GET /api/admin/recharge/pending

Get unmatched/pending recharge requests for manual review.

**Response 200**: `application/json`
```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "studentName": "أحمد محمد",
      "studentPhone": "01098765432",
      "amount": 100.00,
      "senderPhoneNumber": "01098765432",
      "screenshotUrl": "/uploads/recharge-screenshots/abc123.jpg",
      "walletPhoneNumber": "01012345678",
      "status": "Pending",
      "createdAt": "2026-06-25T15:40:00Z",
      "relatedSmsLogs": [
        {
          "id": "guid",
          "sender": "VodafoneCash",
          "body": "تم استلام 100.00 جنيه من ...",
          "receivedAt": "2026-06-25T15:41:00Z",
          "parsedAmount": 100.00,
          "isMatched": false
        }
      ]
    }
  ]
}
```

### POST /api/admin/recharge/{id}/resolve

Approve or reject a recharge request.

**Request**: `application/json`
```json
{
  "action": "approve",
  "rejectionReason": null
}
```
or
```json
{
  "action": "reject",
  "rejectionReason": "لقطة الشاشة غير واضحة"
}
```

### GET /api/admin/sms-sender-filters

Get global SMS sender filter names.

### PUT /api/admin/sms-sender-filters

Update global SMS sender filter names.

**Request**: `application/json`
```json
{
  "filters": ["VodafoneCash", "VF-Cash", "OrangeCash"]
}
```
