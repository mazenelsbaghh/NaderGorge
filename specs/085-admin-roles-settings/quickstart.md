# Quickstart & Verification Guide

## 1. Apply Schema Migrations
Run the following commands in the backend to create and apply the database changes:
```bash
# Generate the migration
dotnet ef migrations add AddPermissionsToRole --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API

# Apply the migration
dotnet ef database update --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API
```

## 2. Launch Local Environment
Run the frontend and backend local servers:
```bash
# In backend directory
dotnet run --project src/NaderGorge.API

# In frontend directory
npm run dev
```

## 3. Verify Features

### Platform Settings Verification
1. Login as Admin (`01000000000` / `Admin@123`).
2. Visit `/admin/settings`.
3. Modify settings (e.g. Platform Name, WhatsApp support link, devices limit).
4. Save settings and verify toast confirmation.
5. In another browser / incognito tab, verify that setting updates take effect (e.g. login is restricted by the new device limit configuration).

### Roles & Permissions Verification
1. Navigate to Settings page → Click on "إدارة الأدوار" tab.
2. Click "إنشاء دور جديد", enter name "مساعد مراجعة" and select "إدارة وتصفية التعليقات" and "إدارة مجتمع الطلاب".
3. Save role.
4. Navigate to Users page → Click "إضافة مستخدم" → Select role "مساعد مراجعة" from dropdown. Fill user details and save.
5. Log in with the newly created assistant account.
6. Verify that they can access Comments and Community sections but get blocked (403 Forbidden) if they try to access other admin sections.

### Maintenance Mode Verification
1. In Settings page, toggle "وضع الصيانة" to active and save.
2. Open a student login view.
3. Verify that student is blocked by the beautiful maintenance screen with the specified custom message.
