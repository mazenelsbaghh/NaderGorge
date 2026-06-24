# Implementation Plan: Parent Tracking System & Mobile Apps

**Branch**: `147-parent-tracking-app` | **Date**: 2026-06-24 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/147-parent-tracking-app/spec.md)
**Input**: Feature specification from `/specs/147-parent-tracking-app/spec.md`

## Summary

This feature implements a Parent Tracking System allowing parents to monitor their child's academic progress via native Kotlin (Android) and Swift (iOS) apps.
The implementation spans:
1. Database Schema migrations to store tracking codes in `StudentProfile` and push notification device tokens in `ParentDeviceToken`.
2. REST API endpoints in C# for student code acknowledgement, parent verification, and fetching detailed student academic stats.
3. Next.js 16 web app integration to display the tracking code via a one-time glassmorphism modal and a permanent header badge.
4. Node.js BullMQ worker with Firebase Admin SDK to push notifications to parent devices upon student academic events.
5. Android app (Kotlin/Jetpack Compose) and iOS app (Swift/SwiftUI with Liquid Glass theme) supporting multi-student switching.
6. Automated builds, compilation checks, and unit tests for both mobile codebases.

## Technical Context

- **Language/Version**: C# 13 (.NET 9.0), TypeScript 5.x, Node.js v20+, Kotlin 1.9 (Android), Swift 6.2 (iOS)
- **Primary Dependencies**: Entity Framework Core 9.0, Firebase Admin SDK, BullMQ, Tailwind CSS, Framer Motion, Jetpack Compose, SwiftUI
- **Storage**: PostgreSQL (Data Store), Redis (Job Queue, distributed caching)
- **Testing**: xUnit (C# backend), Jest (worker), Playwright (Next.js E2E), JUnit/MockK (Android), XCTest (iOS)
- **Target Platform**: Linux (Docker containers), iOS 17+, Android SDK 34+
- **Project Type**: Web Application + Mobile Apps + Worker Microservice
- **Performance Goals**: API response time < 150ms p95, Mobile dashboards render instantly, compile/test completes under 3 minutes
- **Constraints**: Offline capability in mobile apps (view cached dashboard), strict JWT-based authorization

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**:
  - Backend: Added model fields, API controllers, token generators, and EF migrations.
  - Frontend (Next.js): Added modal, header badge, and logic to fetch tracking code and save popup acknowledgment.
  - Worker (Node.js): Added Firebase Admin push notification sender job handler.
  - Database: Updated `StudentProfiles` table, created `ParentDeviceTokens` table.
  - Docker: Verified PostgreSQL and Redis containers. Android compile/test is encapsulated in Gradle Docker container.
- **Automated tests**:
  - C# Backend: `NaderGorge.Application.Tests` and `NaderGorge.Integration.Tests`.
  - Node Worker: `npm test` inside `worker/`.
  - Next.js Web: Playwright E2E tests for modal/badge.
  - Kotlin Android: `./gradlew testDebugUnitTest` run inside Android Gradle container.
  - Swift iOS: `swift test` run on host macOS.
- **Manual QA flows**:
  - Student Web Flow: Check modal presentation on first login, copy code, close, reload.
  - Parent Mobile Flow: Link child using code, verify details are loaded. Add second child, verify list selector and dashboard updates.
- **Docker gate commands**:
  - `docker compose config -q`
  - `docker compose exec backend dotnet ef database update`
- **Phase transition gate**: Next phase cannot start until all build/compile checks pass.

## Project Structure

### Documentation (this feature)

```text
specs/147-parent-tracking-app/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Technical research & choices
├── data-model.md        # Database schema details
├── quickstart.md        # Quickstart & build commands
└── contracts/
    └── parent-api.yaml  # API endpoint contract
```

### Source Code (repository root)

```text
backend/
├── src/NaderGorge.Domain/Entities/StudentProfile.cs
├── src/NaderGorge.Domain/Entities/Notifications/ParentDeviceToken.cs
├── src/NaderGorge.Infrastructure/Services/TokenService.cs
└── src/NaderGorge.API/Controllers/ParentController.cs

frontend/
├── src/components/student/ParentCodePopup.tsx
├── src/components/layout/HeaderParentBadge.tsx
└── src/app/student/shell/StudentShellChrome.tsx

worker/
└── src/jobs/notification-sender.ts

mobile/parent-android/
├── app/src/main/java/com/nadergorge/parent/
│   ├── data/
│   ├── ui/
│   └── service/
└── build.gradle.kts

mobile/parent-ios/
├── NaderGorgeParent/
│   ├── Models/
│   ├── Services/
│   └── Views/
└── Package.swift
```

**Structure Decision**: Selected Option 3 (Mobile + API) since we are implementing native Kotlin (Android) and Swift (iOS) applications alongside the existing backend, frontend, and worker projects.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Backend: `dotnet test backend/NaderGorge.sln --filter "FullyQualifiedName~Parent"`
- Worker: `npm --prefix worker test`
- Frontend: `npx --prefix frontend playwright test tests/e2e/parent-flow.spec.ts`
- Android App: `docker run --rm -v $(pwd)/mobile/parent-android:/app -w /app gradle:8-jdk17-alpine ./gradlew test`
- iOS App: `cd mobile/parent-ios && swift test`

**Docker Gate Required**:
- `docker compose up -d`
- `docker compose ps`
- Health check of backend at `http://localhost:5245/health`

**Manual QA Required**:
- Student logs in to web client. Sees popup. Copies code. Closes modal. Verify it doesn't show again.
- Parent enters code in mobile app. Verify child is linked.
- Parent adds second student. Verify switching between children updates UI details.
- Student submits homework/exam. Verify push notification lands on parent device.

**End-of-Phase Report Format**:
- Implemented scope (Database, API, Web, Worker, Android, iOS).
- Compilation checks outcome (0 errors/warnings).
- Test execution output & coverage.
- Firebase integration verification details.
