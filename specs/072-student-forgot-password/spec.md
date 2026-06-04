# Feature Specification: Student Forgot Password / نسيت كلمة المرور للطالب

**Feature Branch**: `072-student-forgot-password`  
**Created**: 2026-06-04  
**Status**: Draft  
**Input**: User description: "عايز بنفس هويتنا نعمل ف نسيت كلمه المرور بيحط الرقم بتاعوا و بتاع ولي الامر و المحافظظه و الحي ولو حطهم صح بيقولوا حط الباسورد الجذيذ"

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Verify Student Identity via Academic Profile (Priority: P1)

As a Student who forgot their password, I want to verify my identity by providing my phone number, parent's phone number, governorate, and district, so that the platform can verify my records.

**Why this priority**: It is the core security mechanism that authorizes a student to change their password without administrative intervention.

**Independent Test**: Can be tested independently by submitting correct profile information for a registered student to verify that the system approves the credentials and issues a secure reset token.

**Acceptance Scenarios**:

1. **Given** a student is on the login page, **When** they click on the "نسيت كلمة المرور؟" link, **Then** they should be redirected to the `/forgot-password` page.
2. **Given** the student is on `/forgot-password`, **When** they view the page, **Then** they should see a form with the following fields in Arabic:
   - رقم الهاتف (Student's Phone Number)
   - رقم هاتف ولي الأمر (Parent's Phone Number)
   - المحافظة (Governorate - Dropdown populated with Egypt's 27 governorates)
   - المنطقة / الحي (District - Dropdown populated dynamically based on the selected governorate)
3. **Given** the student fills in all verification fields, **When** they submit the verification form with details that exactly match their registered profile (where parent phone matches either their Father's phone, Mother's phone, or secondary parent phone), **Then** they should see the second step of the form: "تعيين كلمة المرور الجديدة" (Set New Password).
4. **Given** the student fills in the verification fields, **When** they submit details that do not match any registered student, **Then** they should see an error message in Arabic: "عذرًا، البيانات المدخلة غير متطابقة مع أي حساب مسجل لدينا." (The entered data does not match any registered account).

---

### User Story 2 - Reset Password with Strong Password Verification (Priority: P2)

As a Student whose identity has been verified, I want to input a new password and confirm it, so that I can securely log back into my account.

**Why this priority**: Completes the password reset process, ensuring the account password is updated and meets standard security complexity rules.

**Independent Test**: Can be tested by submitting a new password after a successful verification step and confirming that the password is changed and allows the user to log in.

**Acceptance Scenarios**:

1. **Given** the student has successfully verified their identity, **When** they are prompted to input their new password, **Then** they should see two password input fields in Arabic:
   - كلمة المرور الجديدة (New Password)
   - تأكيد كلمة المرور الجديدة (Confirm New Password)
2. **Given** the student enters a new password, **When** the new password is less than 8 characters, **Then** they should see a validation error: "يجب أن تتكون كلمة المرور من 8 أحرف على الأقل." (Password must be at least 8 characters).
3. **Given** the student enters a new password and confirmation, **When** the two passwords do not match, **Then** they should see a validation error: "كلمتا المرور غير متطابقتين." (Passwords do not match).
4. **Given** the student inputs a valid, matching password, **When** they click "تأكيد تغيير كلمة المرور" (Confirm Password Change), **Then** the password should be successfully reset, and they should be redirected to the `/login` page with a success toast or alert: "تم تغيير كلمة المرور بنجاح. يمكنك تسجيل الدخول الآن." (Password changed successfully. You can log in now).

---

## Edge Cases

- **Multiple Parent Phone Matches**: The parent phone input must match either `ParentPhone` (father's), `MotherPhone` (mother's), or `SecondaryParentPhone` (secondary contact) of the student profile.
- **District and Governorate Mismatches**: If a user tries to modify the HTML options to submit a district that does not exist in the selected governorate, the system must reject it.
- **Session/Token Expiration**: The password reset token returned upon successful verification must expire after 10 minutes. If the student waits longer than 10 minutes to submit the new password, the reset attempt should fail, prompting them to re-verify.
- **Non-Student Accounts**: If an admin, teacher, or assistant tries to use this forgot password flow, the system must reject it (this flow is strictly for student self-service).

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a public route `/forgot-password` containing a two-step wizard.
- **FR-002**: The page UI MUST match the exact visual style, colors, dark/light theme options, fonts, and interactive ripple effects of the Login and Register pages.
- **FR-003**: The first step MUST collect and validate `PhoneNumber`, `ParentPhone`, `Governorate`, and `District`.
- **FR-004**: The system MUST expose a backend endpoint `/api/auth/verify-reset-fields` that validates if a student exists matching all four fields, checking the parent phone against all three parent columns: `ParentPhone`, `MotherPhone`, and `SecondaryParentPhone`.
- **FR-005**: On successful verification, the backend MUST return a secure, temporary, cryptographically signed token (JWT) containing the user ID, valid for 10 minutes, with a claim/role representing authorization to reset the password.
- **FR-006**: The second step MUST collect `NewPassword` and `ConfirmNewPassword`.
- **FR-007**: The system MUST expose a backend endpoint `/api/auth/reset-password` that accepts the temporary token and the new password, validates the token's signature, checks for expiration, and updates the user's password hash in the database.
- **FR-008**: Passwords MUST be hashed using the BCrypt hashing scheme before saving to the database.

### Key Entities

- **User**: Represents the account credentials (`PhoneNumber` and `PasswordHash`) and profile completion status.
- **StudentProfile**: Contains personal and demographic details associated with the user, including `Governorate`, `District`, `ParentPhone`, `MotherPhone`, and `SecondaryParentPhone`.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Verified students can complete the identity verification and password reset process in under 1 minute.
- **SC-002**: Absolutely 0% chance of resetting another student's password without knowing their exact parent phone, governorate, and district.
- **SC-003**: Temporary reset tokens expire and become invalid after exactly 10 minutes.
- **SC-004**: Password changes are processed and encrypted instantly on the backend, updating in the database under a secure transaction.

---

## Assumptions

- We assume the existing JWT settings (`Issuer`, `Audience`, `Secret`) are sufficient to sign and validate the temporary reset token.
- No SMS verification code (OTP) is requested for this specific simplified forgot password flow, as the four-field profile match is considered the verification factor.
