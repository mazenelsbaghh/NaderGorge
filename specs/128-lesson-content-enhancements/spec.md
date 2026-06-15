# 128 — Lesson Content Management Enhancements

## 1. Overview / نظرة عامة

مجموعة تحسينات على صفحة إدارة محتوى الحصة (Lesson Profile) في لوحة الأدمن تشمل:

1. **تعديل الفيديو بفورم** — بدلاً من `window.prompt`، يظهر فورم مملوء بالبيانات الحالية
2. **تفعيل/إخفاء الفيديو** — زر toggle لإظهار أو إخفاء الفيديو للطالب
3. **رفع الملفات على السيرفر + لينك** — دعم رفع ملف مباشر على دومين `assets` بالإضافة للينك URL الحالي
4. **بروفايل الامتحان المفصل** — صفحة عرض كاملة للامتحان بإحصائيات كل سؤال + إمكانية التعديل والإضافة
5. **حفظ تلقائي للامتحان** — auto-save أثناء إنشاء الامتحان لمنع فقدان البيانات

---

## 2. User Stories / قصص المستخدم

### US-01: تعديل الفيديو بفورم مناسب
**As** an admin  
**I want** to click "Edit" on a video and see a form pre-filled with its current data  
**So that** I can edit title, URL, order, provider, and watch limit conveniently without using browser prompts.

**Acceptance Criteria:**
- Clicking the edit button opens an inline form or modal pre-filled with: title, provider, url/embed, order, maxWatchCount
- Form validates inputs before submission
- On save, the video updates and list refreshes
- Cancel button closes the form without saving

### US-02: تفعيل/إخفاء الفيديو
**As** an admin  
**I want** to toggle a video's visibility so inactive videos don't show to students  
**So that** I can prepare content in advance and control what students see.

**Acceptance Criteria:**
- New `IsActive` boolean field on `LessonVideo` entity (default `true`)
- Toggle button in the video list row (eye/eye-off icon)
- Backend endpoint: `PATCH /api/admin/videos/{id}/toggle-active`
- Frontend student video player hides inactive videos
- Inactive videos appear with a dimmed/badge overlay in admin view

### US-03: رفع الملفات مباشرة + لينك
**As** an admin  
**I want** to upload a file directly (PDF, image, document) OR paste a URL link  
**So that** both methods are available for attaching resources to lessons.

**Acceptance Criteria:**
- `AddResourceForm` has two modes: "رفع ملف" (File Upload) and "لينك مباشر" (URL)
- File upload sends the file to `POST /api/admin/resources/upload` which stores it on the assets domain
- The returned URL is saved as the resource's `fileUrl`
- Both upload and URL link paths coexist side by side (radio/tab toggle)

### US-04: بروفايل الامتحان المفصل
**As** an admin  
**I want** a detailed exam profile page that shows per-question statistics and allows editing  
**So that** I can understand student performance and manage questions easily.

**Acceptance Criteria:**
- "عرض الامتحان" button navigates to `/admin/content/exams/[id]`
- Page shows: exam title, total students who took it, average score, pass rate
- Per-question: correct %, wrong %, most selected wrong answer
- Edit question inline
- Add new question button
- Delete question with confirmation

### US-05: حفظ تلقائي للامتحان
**As** an admin  
**I want** the exam builder to auto-save my progress while I'm creating questions  
**So that** I don't lose my work if I navigate away or the page refreshes.

**Acceptance Criteria:**
- After adding each question, it auto-saves to the backend
- Visual indicator shows save status (saved/saving/error)
- If the exam already exists (has an ID), questions are added incrementally
- If creating a new exam, it creates the exam on first question addition, then adds subsequent questions to it

---

## 3. Functional Requirements / المتطلبات الوظيفية

### FR-01: Video Edit Form
- Replace `window.prompt` with inline form in `LessonVideoList`
- Form fields: title, provider (dropdown), URL/embed, order (number), maxWatchCount (number)
- Populated from the existing video data
- Submit calls `adminService.updateVideo()`

### FR-02: Video IsActive Toggle  
- **Backend**: Add `IsActive` column to `LessonVideo`, new migration
- **Backend**: `PATCH /api/admin/videos/{id:guid}/toggle-active` endpoint
- **Frontend**: Eye/EyeOff toggle in video list
- **Student-side**: Filter out `IsActive = false` videos in lesson content queries

### FR-03: Resource File Upload
- **Backend**: `POST /api/admin/resources/upload` — accepts `IFormFile`, saves to `/uploads/resources/` dir, returns the URL on the assets domain
- **Frontend**: Modify `AddResourceForm` with tab toggle between "رفع ملف" and "لينك مباشر"
- File upload tab shows file picker input + preview
- URL tab remains the current URL input

### FR-04: Exam Profile Page
- **Frontend**: New page at `/admin/content/exams/[id]/page.tsx`
- Uses `getExamDashboard` API (already exists)
- Enhance backend `GetExamDashboardQuery` to include per-question answer stats from `StudentExamAttempt` data
- Add edit/delete question actions inline
- Add "Add Question" form at the bottom

### FR-05: Exam Auto-Save
- Modify `UnifiedAssessmentBuilder` to auto-save after each question is added
- If exam doesn't exist yet, create it on first question, then add questions incrementally
- Show auto-save status indicator (✓ saved, ● saving, ✕ error)

---

## 4. Edge Cases / حالات الحافة

- Video with no URL → don't allow saving
- Toggle active on a video that students are currently watching → no interruption, just hide from list next time
- Upload a 10MB+ file → enforce max file size (10MB)
- No students took the exam → stats show "لا توجد إحصائيات بعد"
- Auto-save fails → show error toast but don't block the user, allow retry

---

## 5. Out of Scope / خارج النطاق

- Drag & drop reordering of videos
- Bulk file upload
- Version history for exams
- Real-time collaborative exam editing
