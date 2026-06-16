# Research Document: E2E Test Flow for Course Content Creation and Access

## 1. Domain Entities & Validation Constraints

Our investigation of the backend database schema and API controllers revealed the following constraints:
- **Teacher Specialization**: The `TeacherProfile.Specialization` string is split on commas. Valid values that map to target grades are `FirstSecondary` (maps to "1st Secondary"), `SecondSecondary` (maps to "2nd Secondary"), and `SecondaryGrade3` (maps to "3rd Secondary"). If they do not match, package creation fails.
- **Subject-Teacher Association**: A teacher can only be assigned to a package if they are linked to the subject via `TeacherSubject` table. We must pass `subjectIds` containing the created subject ID during teacher profile creation.
- **Cascading Access**: Access is cascading: Lesson → ContentSection → Term → Package. A package-level grant allows access to all child lessons.
- **Free Lessons vs Paid Lessons**: Lessons have a `Price` field.
  - If a lesson price is 0, a student has access if they "purchase" the lesson for 0 EGP, generating a `StudentAccessGrant` of type `Lesson`.
  - If a lesson price is greater than 0, the student must buy either the lesson individually, the month section, the term, or the yearly package.

## 2. API Endpoints for the Flow

Below are the exact API endpoints and JSON bodies used in the integration test:

1. **Create Subject**: `POST /api/admin/subjects`
   - Body: `{ "name": "Physics", "description": "Subject for E2E Flow" }`
2. **Create Teacher User**: `POST /api/admin/users`
   - Body: `{ "fullName": "Flow Teacher", "phoneNumber": "20000000088", "password": "password", "role": "Teacher" }`
3. **Create Teacher Profile**: `POST /api/admin/teachers`
   - Body:
     ```json
     {
       "userId": "userId-guid",
       "bio": "Expert physics teacher",
       "specialization": "FirstSecondary",
       "commissionRate": 0.20,
       "contactInfo": "teacher@test.com",
       "subjectIds": ["subjectId-guid"]
     }
     ```
4. **Create Yearly Package**: `POST /api/admin/packages`
   - Body:
     ```json
     {
       "name": "Yearly Physics Package",
       "description": "Full school year",
       "price": 1000,
       "subjectId": "subjectId-guid",
       "targetGrade": "1st Secondary",
       "teacherId": "teacherProfileId-guid"
     }
     ```
5. **Create Term (e.g. Month 1 / Month 2)**: `POST /api/admin/terms`
   - Body: `{ "title": "Month 1", "order": 1, "packageId": "packageId-guid", "price": 100 }`
6. **Create Section**: `POST /api/admin/sections`
   - Body: `{ "title": "Week 1", "order": 1, "termId": "termId-guid", "price": 0 }`
7. **Create Lesson (Free: Price = 0 / Paid: Price > 0)**: `POST /api/admin/lessons`
   - Body: `{ "title": "Free Lesson", "summary": "Intro", "order": 1, "sectionId": "sectionId-guid", "price": 0, "examId": null }`
8. **Create Video**: `POST /api/admin/videos`
   - Body: `{ "title": "E2E Video", "provider": "youtube", "urlOrEmbedCode": "dQw4w9WgXcQ", "order": 1, "limit": 3, "lessonId": "lessonId-guid", "isActive": true }`
9. **Create Inline Exam**: `POST /api/admin/exams/inline`
   - Body:
     ```json
     {
       "title": "Lesson Exam",
       "description": "Solve to pass",
       "passingScore": 5,
       "totalScore": 10,
       "durationMinutes": 30,
       "isMandatory": true,
       "isRandomized": false,
       "target": { "type": "Lesson", "id": "lessonId-guid" },
       "questions": [
         {
           "text": "1+1=?",
           "type": "MCQ",
           "points": 10,
           "order": 1,
           "options": [
             { "text": "2", "isCorrect": true },
             { "text": "3", "isCorrect": false }
           ]
         }
       ]
     }
     ```
10. **Attach Homework**: `POST /api/admin/content/lessons/{lessonId}/homework`
    - Body:
      ```json
      {
        "title": "Lesson Homework",
        "instructions": "Submit solution",
        "isMandatory": true,
        "isRandomized": false,
        "requiredPointsToPass": 6,
        "totalScore": 10,
        "questions": [
          {
            "text": "Solve equation",
            "order": 1,
            "points": 10,
            "type": "Essay",
            "questionType": "Essay"
          }
        ]
      }
      ```
11. **Deduct/Adjust Balance (for Student)**: `POST /api/admin/users/students/{userId}/balance/adjust`
    - Body: `{ "amount": 1200, "reason": "E2E Setup" }`
12. **Student Purchase / Enroll**: `POST /api/student/balance/purchase`
    - Body: `{ "contentType": "Package", "contentId": "packageId-guid" }`
