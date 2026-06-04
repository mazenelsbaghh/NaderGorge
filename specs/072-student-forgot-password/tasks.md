# Spec Kit Preparation Workflow / سير عمل إعداد مواصفات التجهيز

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Student Forgot Password / نسيت كلمة المرور للطالب

**Input**: Design documents from `/specs/072-student-forgot-password/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are OPTIONAL - we will rely on runtime manual verification and compiling checks.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure updates

- [x] T001 [P] Update `ITokenService.cs` in [ITokenService.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Domain/Interfaces/ITokenService.cs) to declare the custom lifetime token generation overload.
- [x] T002 [P] Update `TokenService.cs` in [TokenService.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Infrastructure/Services/TokenService.cs) to implement the custom lifetime token generation overload.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core application logic and API shell setup

- [x] T003 Create `VerifyResetFieldsCommand.cs` in [VerifyResetFieldsCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Auth/Commands/VerifyResetFieldsCommand.cs) representing the command and handler structure.
- [x] T004 Create `ResetPasswordCommand.cs` in [ResetPasswordCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Auth/Commands/ResetPasswordCommand.cs) representing the reset command and handler structure.
- [x] T005 Register endpoints in `AuthController.cs` in [AuthController.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.API/Controllers/AuthController.cs).
- [x] T006 Expose `verifyResetFields` and `resetPassword` client methods in `auth-service.ts` in [auth-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/auth-service.ts).

**Checkpoint**: Foundation ready - API layers are fully declared and ready for detail implementation.

---

## Phase 3: User Story 1 - Verify Student Identity via Academic Profile (Priority: P1) 🎯 MVP

**Goal**: Verify student details (phone, parent phone, governorate, district) and return a temporary token.

**Independent Test**: Use curl or Postman to submit valid profile details and verify that it returns a valid JWT reset token.

### Implementation for User Story 1

- [x] T007 [US1] Implement the validation logic and DB query in [VerifyResetFieldsCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Auth/Commands/VerifyResetFieldsCommand.cs) (validate matching phone number, check parent phone against father/mother/secondary columns, and match governorate/district).
- [x] T010 [US1] Create the `/forgot-password` route page file in [page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/%28public%29/forgot-password/page.tsx) with the base dot-grid background, Cairo font settings, and layout shell matching [page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/%28public%29/login/page.tsx).
- [x] T011 [US1] Implement Step 1 form (Identity Verification) in [page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/%28public%29/forgot-password/page.tsx) including governorate selection list and dynamic district selection dropdown populated via `getDistrictsForGovernorate`.

**Checkpoint**: Identity verification page is fully functional and returns the token on success.

---

## Phase 4: User Story 2 - Reset Password with Strong Password Verification (Priority: P2)

**Goal**: Input new password, validate match/length, and update user password hash in the database.

**Independent Test**: Complete verification step, then input new password, and verify student can successfully log in.

### Implementation for User Story 2

- [x] T012 [US2] Implement validation, password hashing, and DB persistence in [ResetPasswordCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Auth/Commands/ResetPasswordCommand.cs).
- [x] T013 [US2] Implement Step 2 form (Reset Password) in [page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/%28public%29/forgot-password/page.tsx) with validations (minimum 8 characters, confirmation match).
- [x] T014 [US2] Connect Step 2 form submission to `authService.resetPassword` and add a redirect back to `/login` with a success toast notification.
- [x] T015 [US2] Update the "نسيت كلمة المرور؟" link in [LoginForm.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/forms/LoginForm.tsx) to redirect to `/forgot-password` instead of `#`.

**Checkpoint**: Complete password reset flow is active and testable end-to-end.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Code quality checks, compilation checks, styling polish, and documentation updates.

- [x] T016 Run full project lint and build checks (`npm run lint` and `dotnet build`) to ensure there are no warnings.
- [x] T017 Update master plans in `docs/` directory with completed items.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup (Phase 1) completion.
- **User Story 1 (Phase 3)**: Depends on Foundational (Phase 2).
- **User Story 2 (Phase 4)**: Depends on User Story 1 (Phase 3).
- **Polish (Phase 5)**: Depends on all user stories being completed.

### Parallel Opportunities

- Setup tasks T001 and T002 can run in parallel.
- UI layout creation (T010) can run in parallel with API handler creations (T003, T004, T005, T006).
