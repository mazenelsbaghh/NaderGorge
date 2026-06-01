# Quickstart Guide: Birthday Greetings & Video Progression Lock

This guide describes how to run and test the birthday greeting script and verify the video exam progression lock.

## Part 1: Student Birthday Greetings Script

### Setup & Credentials
Ensure your `worker/.env` file contains the database connection and Evolution API variables:
```env
DATABASE_URL=postgresql://postgres:postgres@localhost:5432/nadergorge?schema=public
EVOLUTION_API_BASE_URL=https://evo.n8n-mazen.online
EVOLUTION_API_KEY=your-api-key
EVOLUTION_API_INSTANCE=Nader
```

### Running the Script Standalone
1. Build the worker project:
   ```bash
   cd worker
   npm run build
   ```
2. Run the birthday script:
   ```bash
   node dist/scripts/birthday-congratulator.js
   ```
   Or via the package shortcut:
   ```bash
   npm run congratulate-birthdays
   ```

### Manual Testing / Verification
To verify the birthday script locally:
1. Identify or insert a test student in the database:
   ```sql
   -- Find a student ID
   SELECT "UserId", "DateOfBirth" FROM "student_profiles" LIMIT 1;
   ```
2. Update that student's birth date to match today's date (Egypt timezone):
   ```sql
   UPDATE "student_profiles"
   SET "DateOfBirth" = timezone('Africa/Cairo', NOW())
   WHERE "UserId" = 'STUDENT-GUID-HERE';
   ```
3. Run the script:
   ```bash
   npm run congratulate-birthdays
   ```
4. Verify that a birthday row was inserted into `notification_events`:
   ```sql
   SELECT * FROM "notification_events" WHERE "UserId" = 'STUDENT-GUID-HERE' ORDER BY "CreatedAt" DESC;
   ```
5. Check worker logs to see if it successfully attempted to send the WhatsApp message.

---

## Part 2: Video Exam Progression Lock

### Setup Test Content
1. Navigate to the Admin Lesson Cockpit.
2. Under any lesson, attach an exam to the first video (Video 1).
3. Ensure there is a second video (Video 2) in the same lesson.

### Verification Steps
1. Log in as a student.
2. Navigate to the lesson page: `/student/packages/[packageId]/lessons/[lessonId]`.
3. In the Video Carousel steps:
   - Video 1 step must be active/clickable.
   - Video 2 step must show a lock icon and be disabled.
4. Open Video 1. The player should run normally.
5. Try to switch to Video 2. The carousel should prevent it, or clicking should do nothing.
6. Below Video 1, or in the player, locate the quiz/exam and complete it successfully (achieving the passing score).
7. Go back to the lesson page.
8. Video 2 step should now be unlocked (no lock icon, clickable) and playable.
