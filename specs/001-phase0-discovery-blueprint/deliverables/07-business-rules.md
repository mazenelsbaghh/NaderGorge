# 07 — Business Rules Document

## Watch Control
- **Max Minutes:** Students have a bounded viewing time per video (e.g., Duration * 1.5) to prevent infinite re-watching and account sharing.
- **Max Replays:** Hard limit on the number of times a video can be restarted.
- **Allowed Speeds:** Enforced up to 2x; completely disabling playback if unauthorized DOM manipulation is detected.
- **Skip Policy:** Seeking forward is strictly disabled on the first watch.
- **Completion Threshold:** A video is only marked "completed" if at least 90% of the duration was watched.

## Exam Rules
- **Formats:** Supports MCQ, True/False, and Essay (free-text) questions.
- **Question Bank:** Questions are tagged by Topic, Difficulty, and Content Section for dynamic exam generation.
- **Pass Thresholds:** Each exam defines its own passing percentage (e.g., 50% or 75%).
- **Instant Grading:** MCQs are graded immediately upon submission.
- **Attempt Tracking:** The system records how many times a student needed to pass, logging scores for each attempt.

## Homework Rules
- **Submission States:** Pending → Submitted → Under Review → Graded.
- **Due Dates:** Homework holds strict deadlines. Submitting late flags the student.
- **Review Pipeline:** Essays enter a queue for Assistants. Grades are combined with auto-graded MCQs to form the final score.

## Student Behavior (Categorization)
Based on telemetry, the system mathematically categorizes students:
1. **Committed:** High attendance, passes exams on first try, watches 90%+ of videos.
2. **Average:** Occasional late homework, requires multiple exam attempts, but generally keeps up.
3. **At-Risk:** Fails to watch videos, misses exams, or fails prerequisite gates repeatedly. *Triggers automated dashboard alerts for Follow-up Assistants.*

## Gamification
- **Philosophy:** Motivating, not toxic. Avoids public shaming.
- **Features:**
  - Experience Points (XP) earned for prompt homework and high exam scores.
  - Digital Badges for completing Content Sections perfectly.
  - Platform Leaderboard (Top 100) reset per Content Section to give everyone a fresh chance.

## Progression (Gating)
- **Hard Gates:** A student *cannot* open Lesson B if they have not passed the Homework/Exam for Lesson A.
- **Pathways:** Code types dictate the path. A Lesson Code only demands completion of that lesson's components. A Package Code enforces sequential completion of all lessons within it.

## AI Scope & Boundaries (Phase 4 scope)
AI is strictly bounded to protect academic integrity.
- **In-Scope:**
  - Generating varied questions from the Teacher’s existing question bank.
  - Analyzing a student's past exam data to identify weak points.
  - Providing draft grading for essay questions (to be approved by human assistants).
  - A controlled chatbot answering queries *only* against the Teacher's PDF summaries.
- **Out-of-Scope (Prohibited):**
  - No open-ended web searches.
  - No generated content that bypasses the Teacher's established curriculum.
  - AI does *not* make final grading decisions on failing students without human sign-off.
