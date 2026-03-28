# Research Notes: Exam Dashboard & Timers

This document summarizes the best practices and decisions made for implementing resilient, cheat-proof timers for online examinations within the Nader Gorge platform.

## Topic: Enforcing Server-Side Timers Against Client Disconnections

**Context**: The user explicitly requested: "عايز التايمر لو طلعت او اي خحاجه يتعد عادي", meaning timers must continue running independently of the student's local browser state, preventing refresh-resets or disconnection-pauses.

**Decision**:
We will implement an **Absolute Elapsed Time Tracking** pattern.
1. When a student requests to "Start Exam", the backend commits `StartedAt = DateTime.UtcNow` for their `StudentExamAttempt` row.
2. The backend responds with the `StartedAt` timestamp and the `DurationMinutes` limit to the frontend.
3. The frontend displays a visual countdown by calculating the remaining time locally: `Remaining = ExpiryTime - CurrentTime()`, where `ExpiryTime` is based on the server's provided start time to negate local clock skew.
4. If the frontend is reloaded or the user navigates away and re-enters, the API simply queries the existing active `StudentExamAttempt`, fetching the original `StartedAt`. The frontend recalculates the remaining time, which will correctly show that time passed "while they were gone".
5. Upon sumbission, the server calculates whether `CurrentServerTime <= StartedAt.AddMinutes(DurationMinutes)`. If not, we still accept the answers, but mark them strictly. (Or automatically process submittal if time expired mid-answer).

**Rationale**: 
Trusting the client browser `setTimeout` or `localStorage` leads to trivially bypassed constraints. Background WebSockets or constant pings are heavy for infrastructure and fragile under mobile network conditions. A stateless "StartedAt" check is robust, infinitely scalable, and simple to query.

**Alternatives considered**:
- **Continuous Heartbeat Pings**: Sending a request every 30 seconds to deduct time from the database. *Rejected* due to excessive DB writes and unreliability on mobile.
- **Client-Side `localStorage` check**: *Rejected* because local storage is easily tampered with or cleared by students.
- **Scheduled Background Jobs (BullMQ)**: Scheduling a job to auto-submit the exam upon expiry. *Considered* as an enhancement for later if we see issues, but simply blocking late submissions at the endpoint is more performant than enqueueing millions of short-lived jobs.

## Topic: Exam vs Per-Question Timers

**Context**: Timers need to apply globally (e.g. 60 mins for the exam) and per individual question (e.g. 2 mins per MCQ).

**Decision**:
Track two values:
1. `Exam.DurationMinutes`
2. `ExamQuestion.DurationSeconds`

For the Question-level timer, since questions might be answered independently, the frontend must enforce the strict display transition. The backend simply verifies the final exam time. If we strictly need backend verification of *per-question* time, the frontend must submit the answer to the backend the moment it's clicked, or we risk losing state. Given the current `SubmitExamCommand` structure (submits all answers at once), the per-question timer acts primarily as a UI constraint (auto-advancing the UI to the next question when the timer hits zero). 

**Conclusion**: The frontend will automatically "submit" the answer locally and lock the question when the individual `DurationSeconds` runs out, finally sending the assembled payload at the end. This is a reasonable trade-off to minimize API spam while still enforcing the UX requirement.
