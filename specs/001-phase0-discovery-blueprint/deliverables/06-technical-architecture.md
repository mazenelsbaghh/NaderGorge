# 06 — Technical Architecture Document

## Frontend Stack
- **Framework:** Next.js (App Router)
- **Language:** TypeScript
- **Styling:** Tailwind CSS & Shadcn/UI components
- **State Management:** Zustand (global), React Query (server state & caching)
- **Animations:** Framer Motion (crucial for the prescribed "energetic/youthful" UX)

## Backend Stack
- **Framework:** .NET Web API
- **Language:** C#
- **Architecture:** Modular Clean Architecture (Core, Application, Infrastructure, Presentation)
- **Patterns:** CQRS (Command Query Responsibility Segregation) via MediatR
- **ORM:** Entity Framework Core

## Database
- **Primary Datastore:** PostgreSQL
- **Rationale:** Strict relational integrity needed for financial transactions (code redemptions), academic records, and complex role permissions. 

## Cache Layer
- **Technology:** Redis
- **Uses:**
  - High-speed caching for Content hierarchies (Package/Section/Lesson tree)
  - Rate limiting (DDoS protection and preventing multi-device login abuse)
  - JWT Session management and token blacklisting
  - Leaderboard generation for Gamification
  - Notification buffering

## Background Jobs
- **Hybrid Architecture:** 
  - .NET pushes jobs to Redis queues.
  - A separate **Node.js Worker** running **BullMQ** consumes these jobs.
- **Rationale:** Node.js/BullMQ handles concurrent AI calls, email sending, and long-running video ingestion tasks more efficiently with less overhead than native .NET background services in this specific context.

## Video Strategy
- **Initial Phase:** YouTube embedded (unlisted/private via API).
- **Future Phase:** Migration to secure paid providers (Vimeo, Bunny.net, AWS MediaConvert).
- **Architecture:** The frontend and backend must never speak "YouTube" natively. All video metadata and UI players must interface through a `VideoProviderAbstraction` to allow zero-downtime swapping later.

## Communication Patterns
- **Frontend ↔ Backend:** RESTful HTTP APIs with JWT Bearer authentication.
- **Backend ↔ Redis:** Direct TCP connection (StackExchange.Redis).
- **Backend ↔ Node Worker:** Asynchronous strictly via Redis queues. No direct synchronous HTTP calls from .NET to Node.

## Provider Abstractions
Per the Project Constitution, the system relies on abstractions for all 3rd-party integrations:
1. `IVideoProvider`: Abstraction for getting video streams and metadata.
2. `INotificationProvider`: Abstraction covering SMS (WhatsApp) and Email.
3. `IAIService`: Abstraction for LLM calls (OpenAI, Gemini, Claude).
