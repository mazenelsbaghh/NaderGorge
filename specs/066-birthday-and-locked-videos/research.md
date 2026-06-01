# Research Notes: Student Birthday Greetings & Video Exam Progression

## Decision 1: Birthday Checking Logic and Timezone Handling

### Context
Students have a `DateOfBirth` field stored in the `student_profiles` table as a timestamp. We need to identify students whose birth day and month matches today's date in local Egypt time (`Africa/Cairo`).

### Chosen Approach
Fetch active students from the database via a standalone Node.js script. Instead of doing complex database timezone conversions, the script will:
1. Load all active students and their `DateOfBirth` and `PhoneNumber` from the database.
2. Determine today's month and day in Egypt time zone using JavaScript's native timezone support:
   ```typescript
   const egyptTimeStr = new Date().toLocaleString("en-US", { timeZone: "Africa/Cairo" });
   const egyptDate = new Date(egyptTimeStr);
   const todayMonth = egyptDate.getMonth() + 1; // 1-12
   const todayDay = egyptDate.getDate(); // 1-31
   ```
3. For each student, parse their `DateOfBirth` in UTC/local date and extract month and day:
   ```typescript
   const dob = new Date(student.DateOfBirth);
   const birthMonth = dob.getMonth() + 1;
   const birthDay = dob.getDate();
   ```
4. Handle leap years: If today is March 1st and it is a non-leap year, we also select students born on February 29th to ensure they are congratulated.
5. Create an in-app notification in `notification_events`.
6. Send a WhatsApp message via the Evolution API if credentials are set.

### Alternatives Considered
- **SQL-only date filtering**: Comparing dates directly in PostgreSQL using `EXTRACT` and timezone conversions. While efficient, it is less portable, harder to handle leap-year custom logic, and makes offline/dry-run testing harder. Doing it in JavaScript provides excellent logging and flexibility.

---

## Decision 2: WhatsApp Sending via Evolution API

### Format & API Endpoint
We will use the standard Evolution API endpoint to send a text message:
- **Method**: `POST`
- **URL**: `{EVOLUTION_API_BASE_URL}/message/sendText/{EVOLUTION_API_INSTANCE}`
- **Headers**:
  - `apikey`: `{EVOLUTION_API_KEY}`
  - `Content-Type`: `application/json`
- **Body**:
  ```json
  {
    "number": "201XXXXXXXXX",
    "options": {
      "delay": 1200,
      "presence": "composing"
    },
    "textMessage": {
      "text": "كل عام وأنت بخير يا {Name}! 🎉\nبمناسبة عيد ميلادك، تتمنى لك أسرة أكاديمية الأستاذ نادر جورج عاماً دراسياً مليئاً بالنجاح والتفوق. 🎂✨"
    }
  }
  ```

### Phone Number Format
Phone numbers from the database (e.g. `01012345678`) will be normalized to international format (Egypt prefix `20`) by replacing the leading `0` with `20`, resulting in `201012345678`.

---

## Decision 3: Backend Video Progression Enforcer

### DTO Changes
We will expand the C# backend `VideoDto` in `GetLessonDetailQuery.cs` to include:
- `Guid? ExamId`
- `bool ExamPassed`
- `bool IsExamLocked`

### Logic in `GetLessonDetailQueryHandler`
1. Load all videos for the lesson sorted by `Order` ascending.
2. Load all exam attempts by the student for any exams associated with these videos.
3. Traverse the sorted video list:
   - A video starts as unlocked (`IsExamLocked = false`).
   - If `previousVideoExamUnpassed` is true, the current video becomes locked: `IsExamLocked = true`.
   - If the current video has an `ExamId`, check if the student has a passed attempt (`IsPassed == true`). If not, set `previousVideoExamUnpassed = true` (which will lock all subsequent videos).
