# SignalR Real-time Event Contracts

This document defines the payload format for events sent over the `/hubs/platform` connection.

## 1. NotificationCreated
Sent when a new notification is generated for a user.
- **Recipient**: `User_{userId}`
- **Method name**: `NotificationCreated`
- **Payload**:
```json
{
  "id": "guid-string",
  "title": "عنوان الإشعار",
  "message": "محتوى الإشعار بالتفصيل",
  "createdAt": "2026-06-11T01:30:00Z"
}
```

## 2. BalanceChanged
Sent when a student's balance updates.
- **Recipient**: `User_{userId}`
- **Method name**: `BalanceChanged`
- **Payload**:
```json
{
  "newBalance": 150.0,
  "formattedBalance": "150.00 جنيها"
}
```

## 3. CodeActivated
Sent when a code is redeemed.
- **Recipient**: `User_{userId}`
- **Method name**: `CodeActivated`
- **Payload**:
```json
{
  "codeType": "Package",
  "referenceId": "package-guid-string",
  "message": "تم تفعيل باقة الرياضيات بنجاح"
}
```

## 4. LessonPublished
Sent when a new lesson is added to a package.
- **Recipient**: `Package_{packageId}`
- **Method name**: `LessonPublished`
- **Payload**:
```json
{
  "lessonId": "lesson-guid-string",
  "packageId": "package-guid-string",
  "title": "الدرس الأول: المصفوفات",
  "order": 1
}
```

## 5. VideoReady
Sent when a lesson video finishes processing (AI analysis/chapters/transcripts ready).
- **Recipient**: `Lesson_{lessonId}`
- **Method name**: `VideoReady`
- **Payload**:
```json
{
  "lessonId": "lesson-guid-string",
  "videoId": "video-guid-string",
  "title": "فيديو المصفوفات الأساسية",
  "provider": "vk",
  "providerVideoId": "123456_789"
}
```

## 6. VideoProcessingStarted
Sent when video uploading/processing begins.
- **Recipient**: `Lesson_{lessonId}`
- **Method name**: `VideoProcessingStarted`
- **Payload**:
```json
{
  "lessonId": "lesson-guid-string",
  "videoId": "video-guid-string",
  "status": "Processing"
}
```

## 7. ResourceReady
Sent when a lesson resource (e.g. PDF) is uploaded and scanned, ready to download.
- **Recipient**: `Lesson_{lessonId}`
- **Method name**: `ResourceReady`
- **Payload**:
```json
{
  "lessonId": "lesson-guid-string",
  "resourceId": "resource-guid-string",
  "title": "ملخص درس المصفوفات",
  "fileUrl": "/api/content/resources/download/resource-guid-string"
}
```

## 8. ExtraWatchRequestUpdated
Sent when an extra watch request is approved or rejected by an admin.
- **Recipient**: `User_{userId}`
- **Method name**: `ExtraWatchRequestUpdated`
- **Payload**:
```json
{
  "videoId": "video-guid-string",
  "status": "Approved",
  "allowedWatchCount": 3
}
```

## 9. AiJobProgress
Sent to teachers/admins monitoring AI background jobs.
- **Recipient**: `Role_Admin`, `Role_Teacher`
- **Method name**: `AiJobProgress`
- **Payload**:
```json
{
  "jobId": "video-process:123",
  "progress": 45,
  "status": "Transcribing",
  "message": "جاري استخراج النص وتوليد الفصول..."
}
```
