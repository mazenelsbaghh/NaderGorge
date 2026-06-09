# nader gorge Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-06-09

## Active Technologies
- C# (.NET 9) Backend, TypeScript (Next.js) Frontend + Next.js App Router API Handlers (Proxy), Cheerio/HtmlAgilityPack (for scraping the embed tag), PostgreSQL (Data Store) (034-telegram-video-provider)
- Modify the API endpoints/DTOs to accept `"telegram"` as a valid `provider` string for `LessonVideo`. (034-telegram-video-provider)
- TypeScript (Next.js 16.2.1 / React 19) + Next.js App Router (Server-side API handlers) (035-anti-download-protection)
- N/A (Stateless Token Validation) (035-anti-download-protection)
- C# 13 (.NET 9) Backend, Node.js Worker, TypeScript Frontend + EF Core, `@google/genai` (Node), `fluent-ffmpeg` (Node), BullMQ (Node), StackExchange.Redis (.NET) (037-ai-video-chapters)
- PostgreSQL (Data), Local/S3 Bucket (SRT Files), Redis (Job Queue) (037-ai-video-chapters)
- C# 13, TypeScript, Node.js + BullMQ, Express, `yt-dlp`, .NET API (038-cancel-ai-analysis)
- Redis (BullMQ queues), PostgreSQL (state) (038-cancel-ai-analysis)
- Node.js (v20+), React (Next.js), C# (.NET 9) + BullMQ, @google/genai, tailwindcss (039-ai-agent-chaptering)
- Redis (BullMQ UI State), Local Storage (`.tmp`) for Audio/SRT (039-ai-agent-chaptering)
- C# 13, .NET 9 + Entity Framework Core 9.0+, MediatR (040-fix-ai-analysis-concurrency-bug)
- TypeScript (Next.js 16.2.1 / React 19) + `framer-motion`, existing custom components (`PlayerControls.tsx`, `InteractiveTimeline.tsx`) (042-video-player-chapters)
- N/A (Frontend data merely passed as props from pre-existing backend query) (042-video-player-chapters)
- C# 13 (.NET 9) Backend, TypeScript 5.x Frontend, Node.js v20+ Worker + @google/genai (Node.js SDK), EF Core 9.0 (C#), framer-motion (React) (043-chapter-mindmap-generation)
- PostgreSQL (Data Store) and Local/S3 storage for images (043-chapter-mindmap-generation)
- TypeScript 5.x (Frontend, Node.js Worker), C# 13 / .NET 9 (Backend API) + React (Next.js App Router API Handlers), Entity Framework Core (C#), BullMQ, `@google/genai` (044-two-phase-mindmaps)
- PostgreSQL (Data Store, DB migrations), Redis (BullMQ queue broker and Job Queue backend locking) (044-two-phase-mindmaps)
- TypeScript / Next.js 16.2.1 + `cheerio` (for parsing Telegram embed page) (045-telegram-large-media)
- C# 13, .NET 9 (Backend) | TypeScript, Next.js 16.2.1, React 19 (Frontend) + EF Core 9.0, Next.js App Router API (046-google-drive-provider)
- PostgreSQL (LessonVideo entity) (046-google-drive-provider)
- TypeScript (Next.js 16.2.1), C# 13 (.NET 9) + `Next.js App Router API` (047-google-drive-custom-player)
- TypeScript 5.x, .NET 9 + Next.js App Router, Entity Framework Core (048-vk-video-provider)
- PostgreSQL (LessonVideo DB Table) (048-vk-video-provider)
- C# 13 (.NET 9) Backend, TypeScript 5.x / Next.js 16.2.1 Frontend + Next.js App Router (Frontend Proxy), EF Core 9.0 (Backend Data) (049-rutube-video-provider)
- TypeScript (Next.js 16.2.1 / React 19 Frontend) & C# 13 (.NET 9 Backend API) + Next.js App Router (for Proxy Endpoint), `HTML5 <video>` (051-telegram-direct-stream)
- PostgreSQL (LessonVideo DB Table, minimal modification required - just enum change) (051-telegram-direct-stream)
- TypeScript (strict) / React 19 / Next.js 16.2.1 + Next.js, standard DOM APIs, VK JS API (`https://vk.com/js/api/videoplayer.js`) (052-vk-custom-player)
- PostgreSQL (existing `LessonVideo` support for `provider = "vk"`) (052-vk-custom-player)
- C# 13 / .NET 9, TypeScript 5.x / Next.js 16.2.1 / React 19 + MediatR, Entity Framework Core 9, Next.js App Router, React Query-compatible service layer, Tailwind CSS, existing shared admin components (057-comments-moderation)
- PostgreSQL (new lesson comments table plus moderation metadata) (057-comments-moderation)
- PostgreSQL (new community posts, post comments, and post likes tables plus moderation metadata) (058-student-community)
- TypeScript 5.x / Next.js 16.2.1 / React 19, C# 13 / .NET 9 + Next.js App Router, Tailwind CSS, Axios service layer, MediatR, Entity Framework Core 9 (059-theme-color-customization)
- PostgreSQL for persistent student theme preferences, browser storage for last-known theme mode bootstrap only (059-theme-color-customization)
- TypeScript 5.x / Next.js 16.2.1 / React 19 frontend, C# / ASP.NET Core `net8.0` backend (repo current state) + Next.js App Router, Tailwind CSS, Framer Motion, Axios; MediatR, EF Core 8, PostgreSQL provider (060-package-code-profiles)
- PostgreSQL via EF Core migrations (060-package-code-profiles)
- C# / .NET 8 backend, TypeScript 5.x with Next.js 16.2.1 and React 19 frontend + ASP.NET Core Web API, MediatR, Entity Framework Core, PostgreSQL provider, Next.js App Router, Axios service layer, Tailwind CSS (061-fix-critical-workflows)
- PostgreSQL for relational workflow state; Redis remains available for background and callback-adjacent infrastructure but is not the primary persistence target for this feature (061-fix-critical-workflows)
- PostgreSQL for watch tracking, student profile, exam submission, and extra watch request state (062-fix-data-integrity)
- C# 13 / .NET 9 backend, TypeScript 5.x / Next.js 16.2.1 frontend, Node.js worker present but not directly changed in this feature + ASP.NET Core Web API, MediatR, Entity Framework Core, PostgreSQL provider, in-process memory cache, existing frontend Axios service layer (063-fix-logic-performance)
- PostgreSQL for relational state, in-memory cache for 10-minute platform settings reuse (063-fix-logic-performance)
- PostgreSQL (named volume `pgdata`) + Redis (named volume `redisdata`) + Telegram Bot API (named volume `tgdata`) (064-full-docker-setup)
- TypeScript (strict) — Next.js 16.2.1 / React 19 + C# 13 (.NET 9.0) + Node.js worker (066-birthday-and-locked-videos)
- PostgreSQL (StudentProfile, Users, NotificationEvent, LessonVideo, StudentExamAttempt) (066-birthday-and-locked-videos)
- TypeScript 5.x / Next.js 16.2.1 / React 19 + Next.js App Router, Axios, Zustand (112-surface-login-access-contract)
- N/A (Stateless cookie/Zustand validation) (112-surface-login-access-contract)

- TypeScript (strict) — Next.js 16.2.1 / React 19 + framer-motion ^12.38.0, lucide-react ^1.7.0, clsx + tailwind-merge (via `@/lib/utils`) (033-custom-video-player)

## Project Structure

```text
src/
tests/
```

## Commands

npm test && npm run lint

## Code Style

TypeScript (strict) — Next.js 16.2.1 / React 19: Follow standard conventions

## Recent Changes
- 112-surface-login-access-contract: Added TypeScript 5.x / Next.js 16.2.1 / React 19 + Next.js App Router, Axios, Zustand
- 066-birthday-and-locked-videos: Added Student Birthday Greetings (daily script using Evolution API) and Video Exam Progression locks (backend logic and frontend secure player overlays)
- 064-full-docker-setup: Added PostgreSQL (named volume `pgdata`) + Redis (named volume `redisdata`) + Telegram Bot API (named volume `tgdata`)


<!-- MANUAL ADDITIONS START -->
<!-- SPECKIT START -->
<!-- SPECKIT END -->
<!-- MANUAL ADDITIONS END -->
