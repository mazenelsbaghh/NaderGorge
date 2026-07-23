# nader gorge Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-07-12

## Active Technologies
- C# 13/.NET 9 backend; TypeScript 5.x/Next.js 16.2.7/React 19.2.4 frontend; Node.js worker unchanged. + ASP.NET Core Web API, MediatR, FluentValidation, EF Core 9/Npgsql, SignalR 9, Redis backplane, Axios, Zustand, Tailwind CSS; evaluate `@tanstack/react-query` as the single query cache. (160-employee-realtime-refresh)
- PostgreSQL for user authorization/version and durable outbox state; Redis for SignalR backplane/ephemeral coordination; browser memory/local auth storage for session bootstrap; no worker storage change. (160-employee-realtime-refresh)

- (131-homework-progression-fixes)

## Project Structure

```text
src/
tests/
```

## Commands

# Add commands for 

## Code Style

: Follow standard conventions

## Recent Changes
- 160-employee-realtime-refresh: Added C# 13/.NET 9 backend; TypeScript 5.x/Next.js 16.2.7/React 19.2.4 frontend; Node.js worker unchanged. + ASP.NET Core Web API, MediatR, FluentValidation, EF Core 9/Npgsql, SignalR 9, Redis backplane, Axios, Zustand, Tailwind CSS; evaluate `@tanstack/react-query` as the single query cache.
- 147-parent-tracking-app: Added [if applicable, e.g., PostgreSQL, CoreData, files or N/A]

- 131-homework-progression-fixes: Added

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
