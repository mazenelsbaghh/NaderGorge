# Quickstart Guide: AI Live Support Actions & Verification

This guide explains how to start, build, and verify the AI support actions and guest verification flow.

## 1. Local Development Setup

1. **Verify Backend Build**:
   ```bash
   cd backend
   dotnet build
   ```

2. **Verify Worker Build**:
   ```bash
   cd worker
   npm install
   npm run build
   ```

3. **Verify Frontend Build**:
   ```bash
   cd frontend
   npm run build
   ```

## 2. Running Automated Tests

Run the backend unit tests to verify the C# logic:
```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~LiveSupport"
```

## 3. Manual Verification Steps

1. Log in to the Admin Dashboard at `/admin/live-support/ai`.
2. Under "الإجراءات وقاعدة القرار" (Settings & Policy), check the actions you want to enable (e.g. `student.lesson.unlock`).
3. Click "حفظ ونشر وتفعيل" (Save, Publish & Enable).
4. Start a chat as a student and ask to open a locked lesson.
5. Verify that the AI displays a confirmation card in the chat.
6. Click "نعم، متأكد" and verify that the lesson unlocks and can be accessed.
7. Ask for a human support agent. Verify that a confirmation card appears. Click "لا، استمر مع المساعد" and verify the AI remains active and continues to respond.
