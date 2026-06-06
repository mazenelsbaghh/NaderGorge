# Research Notes: Assistant Profile & Egypt Timezone Localization

This document consolidates the technical research and architecture decisions for resolving the assistant student-role bug, audit logs profile view, and Egypt timezone localization.

---

## 1. Role Check Normalization (Fixing Assistant vs Student Bug)

### Problem
In `LoginForm.tsx`, `login/page.tsx`, and `MaintenanceGuard.tsx`, `isStaff` checks only look for specific strings like `'Admin'`, `'Teacher'`, and `'Assistant'`. Dynamic assistant roles (like `'ntu'` or `'رفع'`) are not in this hardcoded list. Thus, the system misclassifies these assistants as `Student` (since it falls back to Student in `normalizeRole`), leading to incorrect routing and blocking them during maintenance mode.

### Solution & Rationale
We will define `isStaff` as:
- A user is staff if their roles array does not include `'Student'`.
In the system database seeds (`Seeder.cs`), the only role assigned to non-staff is `"Student"`. Therefore, any user who holds roles and does not have the `"Student"` role is by definition a staff member (administrator, teacher, or educational assistant).

This is a robust, clean, and future-proof design that automatically supports any future dynamic assistant roles.

---

## 2. Assistant Activity Profile (Audit Logs)

### Problem
There is currently no endpoint or UI for administrators to read from the `AuditLogs` database table. Audit logs are only written. We need a way to fetch and display audit logs performed by a specific assistant.

### Technical Details & Queries
We will implement:
- **Backend Query**: `GetUserAuditLogsQuery(Guid UserId)` returning a list of `UserAuditLogDto`.
- **Query Handler**: In `GetUserAuditLogsQueryHandler`, query `_db.AuditLogs` filtered by `PerformedByUserId == request.UserId` and order them by `CreatedAt DESC`.
- **API Controller Endpoint**: Add a `[HttpGet("users/{id:guid}/audit-logs")]` endpoint in `AdminController.cs`.
- **Frontend Service**: Add `getUserAuditLogs(id: string)` in `admin-service.ts`.
- **Frontend View**: Inside `/admin/users/page.tsx`, add an `AssistantProfileModal` that renders when clicking on any user whose normalized role is `'Assistant'` or `'Admin'`. It will render a chronological timeline of actions performed by that user.

### Action Translation mapping
To make the audit log highly professional and user-friendly, action names will be translated to Arabic on the frontend:
- `AdjustBalance` $\rightarrow$ "تعديل رصيد الطالب"
- `OverrideVideoLimit` $\rightarrow$ "تجاوز حد المشاهدة للفيديو"
- `ToggleStudentSystemAccess` $\rightarrow$ "تعديل حالة وصول الطالب للنظام"
- `ResetWatchLimit` $\rightarrow$ "إعادة تعيين حد المشاهدة"
- `AdjustGamificationPoints` $\rightarrow$ "تعديل نقاط الطالب"
- `ApproveWatchRequest` $\rightarrow$ "الموافقة على طلب مشاهدة إضافية"
- `AddStudentNote` $\rightarrow$ "إضافة ملاحظة للطالب"
- `DeleteStudentNote` $\rightarrow$ "حذف ملاحظة الطالب"
- `UpdateStudentProfile` $\rightarrow$ "تحديث الملف الشخصي لطالب"
- `DisconnectStudentDevice` $\rightarrow$ "فصل جهاز الطالب"
- `RemoveDevice` $\rightarrow$ "حذف جهاز مسجل"
- `CreateUser` $\rightarrow$ "إنشاء مستخدم جديد"
- `UpdateUserStatus` $\rightarrow$ "تحديث حالة المستخدم"
- `UpdateUserRoles` $\rightarrow$ "تحديث أدوار المستخدم"
- Fallback $\rightarrow$ Keep original string

---

## 3. Egypt Timezone Localization

### Problem
Date and time strings throughout the app are formatted using the user's browser-local timezone (via `toLocaleString('ar-EG')`, `Intl.DateTimeFormat`, etc.) or in UTC. We need a way to guarantee all dates display in Egypt local time (`Africa/Cairo`) across the entire stack.

### Technical Approach
1. **Frontend Global Polyfill/Override**:
   We will create a bootstrap file `frontend/src/lib/timezone-bootstrap.ts` that intercepts the global `Intl.DateTimeFormat` constructor and the `Date.prototype.toLocaleString` / `toLocaleDateString` / `toLocaleTimeString` methods. If no `timeZone` property is passed in the options, it will automatically inject `{ timeZone: 'Africa/Cairo' }`.
   This ensures that:
   - All browser date/time rendering automatically conforms to Egypt Time.
   - Next.js server-side rendering (SSR) of dates also conforms to Egypt Time (preventing React hydration mismatches, since the server and client will evaluate date text identically).
2. **Docker TZ Environment Variable**:
   In `docker-compose.yml`, we will add `TZ: Africa/Cairo` to the `environment` section of the containers. This ensures the operating system clock of the backend API, DB, and background workers align perfectly with Cairo local time (especially crucial for scheduled jobs like the daily birthday script).
