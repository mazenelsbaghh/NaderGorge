# Quickstart & Verification: watch-requests-refinements-and-repurchases

## 1. Quick Verification Commands
### Backend Unit Tests
Run backend tests to verify core request resolution, balance deductions, and repurchase behaviors:
```bash
cd backend
dotnet test
```

### Frontend Build
Run Next.js production build to verify TypeScript type checking and compilation:
```bash
cd frontend
npm run build
```

## 2. Manual Verification Checklist
1. **Admin Watch Requests Dashboard**:
   - Resolve a pending request as Approved with custom added views (e.g. +3). Verify the new limit increases correctly.
   - Click the "تعديل القرار" button on any resolved request. Change it to Rejected. Verify student's limit decrements and the video locks.
2. **Student Lesson Repurchase**:
   - Exhaust views on a video to trigger the locked screen.
   - Verify the emerald "شراء الحصة مجدداً" button appears with the lesson price.
   - Click it, confirm, and verify watch counts reset to 0 and playback unlocks.
