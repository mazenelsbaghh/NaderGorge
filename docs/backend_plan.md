# Backend Master Plan

**Last Updated**: 2026-06-04

---

## Active Plans

### Student Forgot Password Endpoints (2026-06-04)
- [x] Add `VerifyResetFieldsCommand` to authenticate student details via student phone, date of birth, governorate, and district.
- [x] Issue a 10-minute temporary JWT token containing a `PasswordReset` claim/role upon successful verification.
- [x] Add `ResetPasswordCommand` to validate the reset token and update the user's password hash in the database using BCrypt.
- [x] Expose endpoints in `AuthController.cs`:
  - `POST /api/auth/verify-reset-fields`
  - `POST /api/auth/reset-password`
- [x] Write FluentValidation rules and unit test commands.

---

## History
- Initialized backend master plan directory.
