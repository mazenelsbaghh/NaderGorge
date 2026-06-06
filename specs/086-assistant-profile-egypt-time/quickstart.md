# Quickstart & Verification: Assistant Profile & Egypt Timezone Localization

This guide details how to verify and test the changes introduced for fixing assistant classification, audit logs, and timezone localization.

---

## 1. Local Testing Steps

### 1.1 Running the local development environment
To boot up the complete development environment with Docker:
```bash
make build
make up
```

Ensure all containers are healthy:
```bash
make ps
```

---

## 2. Verification Checklist

### 2.1 Role Fix Verification
1. Create a custom assistant user (e.g. name: "مساعد الرفع", username/phone: "01000000002", password: "Password@123") in Settings page or via `/admin/users` clicking **إضافة مستخدم** and assigning a custom role (e.g. `"ntu"`).
2. Log out and try logging in as "01000000002" with "Password@123".
3. Verify the login routes to `/admin` dashboard instead of `/student`.
4. Verify the row for this assistant in the users list shows `"مساعد تعليمي"` or the custom role name in the Role column, instead of `"طالب"`.

### 2.2 Assistant Audit Logs Verification
1. Log in as an administrator.
2. Go to the Users tab `/admin/users`.
3. Locate the row for the custom assistant created above and click on it.
4. Verify the `AssistantProfileModal` drawer/modal opens.
5. Verify it contains the metadata (name, phone, role) and a "سجل النشاطات" (Activity Log) tab displaying a timeline of their performed actions.
6. Perform some state changing operation (e.g. adjust a student's balance, reset a watch limit) as that assistant.
7. Click the assistant again in the users list and verify the activity is listed in their timeline in Arabic.

### 2.3 Timezone Verification
1. Change your operating system timezone to something outside Egypt (e.g., GMT/UTC, Europe/Berlin, Eastern Standard Time).
2. Refresh the browser and visit the admin panel or student panel.
3. Check dates displayed in tables (e.g., watch requests, comments, balance transaction timestamps).
4. Verify they display the exact local time in Egypt (GMT+2 or GMT+3 depending on standard/DST).
5. Open browser developer console and run:
   ```javascript
   new Date().toLocaleString('ar-EG')
   ```
   Verify it outputs in the Cairo timezone (corresponds to Egypt local time).
