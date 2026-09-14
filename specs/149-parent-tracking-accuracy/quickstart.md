# Quickstart: Parent Tracking Accuracy

## Preconditions

- A linked parent token for a student profile.
- Test content with at least two teachers.
- Active access grants for one or more package/term/section/lesson/video targets.
- Watch events, exam attempts, homework submissions, and balance transactions for the linked student.

## Automated Verification

1. Run focused backend parent tests:

   ```bash
   dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter Parent
   ```

2. Run backend compile:

   ```bash
   dotnet build backend/NaderGorge.sln --no-restore
   ```

3. Build the Android parent app using the persistent builder:

   ```bash
   make build-mobile-android-offline
   ```

4. Optional Docker gate:

   ```bash
   docker compose config -q
   make up
   curl -f http://localhost:5245/api/health
   make ps
   ```

## Manual QA

1. Open the parent Android app with a linked student.
2. Open `المشاهدات`, select Teacher A, and confirm only Teacher A purchased lessons appear. Repeat for Teacher B.
3. Confirm lessons with no watch activity still appear with zero metrics.
4. Open `الامتحانات`, select a teacher, and confirm exams tied to that teacher's purchased lessons appear with `NotStarted`, `Passed`, or `Failed` status.
5. Open an attempted exam and confirm mistakes show the question, student answer, correct answer, and correction where available.
6. Open `الواجبات`, select a teacher, and confirm `NotSubmitted`, `InProgress`, `PendingReview`, `Graded`, and `Missed` homework states appear with the same review behavior as exams where valid.
7. Open `الرصيد` and compare current balance plus newest transactions against the authoritative student balance record.
8. Negative check: deactivate or expire a grant, refresh parent details, and confirm the related teacher/content no longer appears.

## Expected Result

- The same teacher list drives watch logs, exams, and homework.
- Teacher filtering is based on purchased lesson/package ownership.
- Empty or partial data does not crash the Android app.
- Balance matches the authoritative ledger.
