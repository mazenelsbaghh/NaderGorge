# Implementation Plan: Student Forgot Password / نسيت كلمة المرور للطالب

**Branch**: `072-student-forgot-password` | **Date**: 2026-06-04 | **Spec**: [/specs/072-student-forgot-password/spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/072-student-forgot-password/spec.md)
**Input**: Feature specification from `/specs/072-student-forgot-password/spec.md`

---

## Summary
Add a secure "Forgot Password" feature for students. It verifies their identity by matching their phone number, a parent's phone number, governorate, and district. Upon successful validation, the backend issues a 10-minute temporary signed JWT token. The student then submits their new password along with this token to update their credentials. The UI is designed RTL-first in Arabic and matches the existing premium "Editorial Scholar" theme.

---

## Technical Context

- **Language/Version**: C# 13 (.NET 9.0), TypeScript 5.x / Next.js 16.2.1
- **Primary Dependencies**: MediatR 12.4.1, FluentValidation 11.11.0, BCrypt.Net-Next 4.0.3, System.IdentityModel.Tokens.Jwt 8.3.0, Framer Motion, Lucide React
- **Storage**: PostgreSQL (EF Core 9.0.6)
- **Testing**: Playwright / Unit tests
- **Target Platform**: Docker, Modern Browsers
- **Project Type**: Web Application (React/Next.js) & Web Service (.NET 9)
- **Performance Goals**: Verification and reset API response time < 500ms
- **Constraints**: 10-minute token expiration limit; Rate limit applied to auth flow (10 requests/minute per IP)
- **Scale/Scope**: Available to all registered student accounts

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Principle I: Modular Clean Architecture**: Complies. Changes are divided cleanly: Endpoints in API, commands/handlers in Application, interface changes in Domain, implementation in Infrastructure, and Next.js frontend pages/services.
- **Principle II: Provider Abstraction First**: Complies. All auth tokens are issued via the `ITokenService` abstraction.
- **Principle III: Security & Access Control by Default**: Complies. Token validation is signed via security keys. Passwords are encrypted using BCrypt. Validation schemas exist on both frontend (Zod) and backend (FluentValidation).
- **Principle VIII: Premium Editorial Design System**: Complies. UI implements Cairo font, HSL variables, glassmorphic panel, and dark/light toggling. It uses the same page structure and components as the Registration and Login layouts.

---

## Project Structure

### Documentation (this feature)

```text
specs/072-student-forgot-password/
├── plan.md              # This file
├── research.md          # Research findings and decisions
├── data-model.md        # Database columns used and JWT claim layout
├── quickstart.md        # Manual testing instructions
├── contracts/
│   └── auth.md          # API endpoints specs
└── tasks.md             # Implementation tasks checklist (Created in Phase 3)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   └── Interfaces/
│   │       └── ITokenService.cs          # Update: Add custom lifetime overload
│   ├── NaderGorge.Infrastructure/
│   │   └── Services/
│   │       └── TokenService.cs           # Update: Implement custom lifetime overload
│   ├── NaderGorge.Application/
│   │   └── Features/
│   │       └── Auth/
│   │           └── Commands/
│   │               ├── VerifyResetFieldsCommand.cs   # New: Verify profile info command
│   │               └── ResetPasswordCommand.cs        # New: Reset password command
│   └── NaderGorge.API/
│       └── Controllers/
│           └── AuthController.cs         # Update: Add the 2 new endpoints
 
frontend/
├── src/
│   ├── services/
│   │   └── auth-service.ts               # Update: Add API client service requests
│   └── app/
│       └── (public)/
│           └── forgot-password/
│               └── page.tsx              # New: The forgot password page wrapper
```

**Structure Decision**: Web application option (separated into backend C# projects and Next.js frontend app).

---

## Complexity Tracking

*No constitution violations detected. Standard Clean Architecture and secure JWT authentication patterns are used.*
