# Quickstart Guide: Student Forgot Password

This guide explains how to start, test, and verify the student forgot password feature in local development.

## 1. Prerequisites & Environment

Make sure your local environment is configured by checking your `.env` file at the root.

```bash
# Verify config
cat .env
```

Ensure the backend, database, and cache are running. You can run all infrastructure natively or via Docker:

```bash
# Infrastructure native start
make dev
```

---

## 2. API Verification (Manual testing via HTTP)

### Step 1: Verify Student Profile Details

Send a POST request to verify the student's profile:

```bash
curl -X POST http://localhost:5245/api/auth/verify-reset-fields \
  -H "Content-Type: application/json" \
  -d '{
    "phoneNumber": "01000000002",
    "parentPhone": "01100000002",
    "governorate": "القاهرة",
    "district": "المعادي"
  }'
```

This should return the `resetToken`.

### Step 2: Reset the Password

Using the `resetToken` received above:

```bash
curl -X POST http://localhost:5245/api/auth/reset-password \
  -H "Content-Type: application/json" \
  -d '{
    "token": "YOUR_RESET_TOKEN_HERE",
    "newPassword": "newSecurePassword123"
  }'
```

---

## 3. Frontend Verification

1. Start the Next.js development server:
   ```bash
   make frontend
   ```
2. Navigate to `http://localhost:8738/login`.
3. Click **"نسيت كلمة المرور؟"** under the password input.
4. Fill in the student phone, parent phone, governorate, and district.
5. Click "التالي".
6. Fill in the new password and confirm it.
7. Click "تأكيد" and verify redirection to `/login` with the success notification.
