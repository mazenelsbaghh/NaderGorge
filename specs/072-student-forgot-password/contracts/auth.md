# API Contracts: Authentication

## 1. Verify Password Reset Fields

Verifies the student's profile details and issues a temporary token.

- **URL**: `/api/auth/verify-reset-fields`
- **Method**: `POST`
- **Headers**:
  - `Content-Type`: `application/json`
  - `X-Correlation-Id`: `[Correlation UUID]` (optional)
- **Rate Limit Policy**: `auth` (10 requests/minute per IP)

### Request Body

```json
{
  "phoneNumber": "01012345678",
  "parentPhone": "01112345678",
  "governorate": "القاهرة",
  "district": "المعادي"
}
```

### Response (200 OK - Successful Verification)

```json
{
  "success": true,
  "data": {
    "resetToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  },
  "message": "Verification successful"
}
```

### Response (400 Bad Request - Validation Error or Details Mismatch)

```json
{
  "success": false,
  "data": null,
  "message": "عذرًا، البيانات المدخلة غير متطابقة مع أي حساب مسجل لدينا."
}
```

---

## 2. Reset Password

Accepts the temporary signed token and updates the student's password.

- **URL**: `/api/auth/reset-password`
- **Method**: `POST`
- **Headers**:
  - `Content-Type`: `application/json`
  - `X-Correlation-Id`: `[Correlation UUID]` (optional)
- **Rate Limit Policy**: `auth` (10 requests/minute per IP)

### Request Body

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "newPassword": "secureNewPassword123"
}
```

### Response (200 OK - Successful Password Change)

```json
{
  "success": true,
  "data": null,
  "message": "تم تغيير كلمة المرور بنجاح. يمكنك تسجيل الدخول الآن."
}
```

### Response (400 Bad Request - Invalid/Expired Token or Validation Failure)

```json
{
  "success": false,
  "data": null,
  "message": "رمز إعادة التعيين منتهي الصلاحية أو غير صالح."
}
```
