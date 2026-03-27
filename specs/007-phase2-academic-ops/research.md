# Phase 0 Research & Decisions

## Context

Phase 2 introduces a large scope of academic operations:
1. **Homework Grading Queue**: Assistant reviews and essay grading.
2. **Commitment Engine**: Nightly evaluation of "Committed / Average / At-Risk" states.
3. **Notification Engine**: Delivering triggered alerts via UI and SMS.
4. **Gamification Engine**: Updating Points and Streaks.

## Research Outcomes & Decisions

### Decision 1: Background Jobs using BullMQ vs. Plain Redis `brpop`
**Decision**: Adopt **BullMQ** incrementally for new Phase 2 jobs inside the Node Worker.
**Rationale**: The current implementation of the node worker (`worker/src/index.ts`) uses simple `redis.brpop('code-generation-queue', 0)`. While effective for simple queues, Phase 2 requires scheduled jobs (nightly sweeps for the Commitment Engine), delayed jobs (send a warning 2 hours after inactivity), and retries for failed SMS notifications. BullMQ provides these primitives out-of-the-box using the exact same Redis instance.
**Alternatives Considerered**: Continuing to use plain `brpop` with custom cron scripts. Rejected as it violates the `.specify/memory/constitution.md` and `plan.md` explicit mandate to use BullMQ for background orchestration and leads to brittle retry mechanics.

### Decision 2: Gamification Points Tracking
**Decision**: Calculate and persist the static aggregate data in PostgreSQL `StudentGamification` table with Redis fast-read fallback for leaderboards.
**Rationale**: Points and streaks require strongly consistent relationships but also fast read speeds for leaderboard sorting. Implementing the leaderboard using Redis `ZADD` (Sorted Sets) while backing up the "ledger" of points awarded in Postgres ensures rapid querying but transactional reliability.
**Alternatives Considerered**: Computing purely from PostgreSQL views on-the-fly. Rejected due to the performance constraint of sorting points across thousands of users concurrently.

### Decision 3: "Homework" Data Linking
**Decision**: Implement a flexible `Homework` entity that connects one-to-one or one-to-many to `Lesson`.
**Rationale**: A lesson may have a required assignment containing multiple questions (both MCQ and Essay). 
**Alternatives Considerered**: Creating completely decoupled assessments. Rejected as `plan.md` binds homework to specific lessons for gating content progression.

### Decision 4: Event-Driven Triggers
**Decision**: Use simple standard .NET Application level pub/sub (e.g. MediatR Notifications) to detect events (`HomeworkMissedEvent`, `ExamFailedEvent`), which format JSON payloads and enqueue into Redish/BullMQ.
**Rationale**: Adhering to the modular clean architecture requires minimizing synchronous deep calls between `Gamification`, `Homework`, and `Notification` services. Emitting an event ensures the REST API remains under the ~500ms constraint, and the BullMQ worker asynchronously processes the heavy SMS delivery or Parent Alert Generation.
