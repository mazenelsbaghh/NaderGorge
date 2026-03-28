# Quickstart Guide: Phase 2 — Academic Operations

## Overview
You are building the **Homework System, Assistant Workflow, Gamification Engine, and Notification Queue** for the Fluent Focus Academy (Phase 2). This turns the static content site into an active academic management platform.

## Pre-requisites & Setup

### Database Updates
1. Apply the new Entity Framework Core migrations to create the following tables:
   - `Homework`, `HomeworkQuestions`, `HomeworkSubmissions`, `HomeworkAnswers`
   - `GamificationActionLogs`, `StudentBadges`
   - `WarningEvents`, `NotificationEvents`
   - `AssistantTaskQueue`

*Example Command*:
```bash
cd backend/src/NaderGorge.Infrastructure
dotnet ef migrations add AddPhase2AcademicOps
dotnet ef database update
```

### Redis / BullMQ Setup
1. Ensure the Node.js worker has the `bullmq` dependency (update `package.json` in `worker/`).
2. Add a `bullmq` connection string to `.env` if it differs from the default `ioredis` one.

*Example Command*:
```bash
cd worker
npm install bullmq ioredis
```

## First Steps to Execute

1. **Entity Models (Backend)**: Create the C# Domain Models and aggregate roots.
2. **DTOs & Controllers (Backend)**: Scaffold the new REST APIs mapped in `api-contracts.md`.
3. **Frontend Gamification State (React)**: Update the `StudentService` to fetch the Gamification points and show them globally in the Next.js header.
4. **Homework Component (Frontend)**: Design the `HomeworkView.tsx` component inside lesson modules so students can take MCQ/Essays directly below the video.
5. **Worker Sweeps (Node.js)**: Implement the cron job script in the Node worker that runs a SQL query nightly to flag students as `AtRisk` if they miss homeworks.
6. **Assistant Dashboard (Frontend)**: Create the Assistant-specific navigation item and task queue page.
