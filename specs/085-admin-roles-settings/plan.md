# Implementation Plan: Admin Roles and Settings / الأدوار وإعدادات المنصة

**Branch**: `085-admin-roles-settings` | **Date**: 2026-06-06 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/085-admin-roles-settings/spec.md)
**Input**: Feature specification from `/specs/085-admin-roles-settings/spec.md`

## Summary

The objective is to fix and enhance the Admin Settings page in the Nader Gorge platform. We will:
1. Provide a comprehensive UI to manage all key system-wide platform settings (Name, contact, WhatsApp/YouTube/Telegram links, devices limit, watermarking toggle/opacity, maintenance mode toggle & message).
2. Enable custom administrative Roles & Permissions management, storing permissions dynamically in the DB.
3. Allow selecting these custom roles when adding users or updating their access.
4. Secure backend APIs dynamically using permission claims.
5. Redirect students when Maintenance Mode is enabled.

## Technical Context

**Language/Version**: C# 13 (.NET 9.0), TypeScript 5.x, React 19, Next.js 16.2.1  
**Primary Dependencies**: EF Core 9.0, MediatR, Axios  
**Storage**: PostgreSQL (PlatformSettings, Roles tables)  
**Testing**: Native compilation, runtime verification  
**Target Platform**: Linux server (Docker) / local developer machine  
**Performance Goals**: Setting caching invalidated instantly on save, page load under 200ms  
**Constraints**: Cairo font, RTL layout, glassmorphic admin cards, Cairo typography  

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: Handled through proper layering (Domain, Application, Infrastructure, API).
- **Security & Access Control**: Handled via dynamic role permissions checking in API custom authorization filter.
- **Single-Flow Registration & UX Simplicity**: Maintenance page blocks student layout routes clearly.
- **Premium Editorial Design System**: Cairo typography, no-line rule, admin CSS tokens.

## Project Structure

### Documentation (this feature)

```text
specs/085-admin-roles-settings/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Technical design options
├── data-model.md        # Database schema modifications
└── quickstart.md        # How to run and verify
```

### Source Code

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   │   └── Role.cs (Modified: Add PermissionsJson column)
│   │   └── Enums/
│   │       └── RoleType.cs
│   ├── NaderGorge.Infrastructure/
│   │   ├── Data/
│   │   │   └── AppDbContext.cs (Modified: Configure Role PermissionsJson mapping)
│   │   ├── Migrations/ (New Migration)
│   │   └── Services/
│   │       └── TokenService.cs (Modified: Load and inject permissions claims)
│   ├── NaderGorge.Application/
│   │   ├── Common/
│   │   │   ├── PlatformSettingKeys.cs (Modified: Register new settings keys)
│   │   │   └── CachedPlatformSettings.cs (Modified: Parse new settings)
│   │   └── Features/Admin/
│   │       ├── Commands/
│   │       │   ├── AdminCreateUserCommand.cs (Modified: Load dynamic roles)
│   │       │   ├── CreateRoleCommand.cs (New: Create custom role)
│   │       │   ├── UpdateRoleCommand.cs (New: Edit custom role)
│   │       │   └── DeleteRoleCommand.cs (New: Delete custom role)
│   │       └── Queries/
│   │           ├── GetPlatformSettingsQuery.cs (Modified: Add default values)
│   │           └── ListRolesQuery.cs (New: Fetch database roles)
│   └── NaderGorge.API/
│       ├── Controllers/
│       │   ├── AdminController.cs (Modified: Add roles endpoints & hasPermission checks)
│       │   └── PublicController.cs (Modified: Add public settings endpoint)
│       └── Extensions/
│           └── HasPermissionAttribute.cs (New: Custom permissions auth filter)

frontend/
├── src/
│   ├── app/
│   │   ├── admin/
│   │   │   └── settings/
│   │   │       └── page.tsx (Modified: Render tabs, settings forms, roles list & editor)
│   │   └── student/
│   │       └── layout.tsx (Modified: Enforce MaintenanceGuard check)
│   ├── components/
│   │   ├── admin/
│   │   │   └── AddUserDrawer.tsx (Modified: Load dynamic roles)
│   │   └── layout/
│   │       └── MaintenanceGuard.tsx (New: Fullscreen maintenance display)
│   └── services/
│       └── admin-service.ts (Modified: Add settings and roles API calls)
```

**Structure Decision**: Web application option (frontend Next.js and backend ASP.NET Core) following existing repository layers.
