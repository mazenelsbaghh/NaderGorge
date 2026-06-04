# Technical Design: Data Model / البيانات

## Existing Relational Tables Utilized

This feature is stateless and leverages existing database schemas. No database migrations or schema updates are required.

### 1. `Users` Table
- **PhoneNumber** (string, required): Used to find the student account attempting the password reset.
- **PasswordHash** (string, required): Updated with the new BCrypt password hash upon successful token validation.
- **IsActive** (bool, required): The user account must be active.

### 2. `StudentProfiles` Table
- **ParentPhone** (string, nullable): Father's primary phone.
- **MotherPhone** (string, nullable): Mother's phone.
- **SecondaryParentPhone** (string, nullable): Secondary parent phone.
- **Governorate** (string, required): Must match the Egyptian governorate input.
- **District** (string, nullable): Must match the district input.

### 3. `Roles` & `UserRoles` Tables
- **RoleType.Student**: The forgot password self-service flow is restricted only to users with the `Student` role to prevent unauthorized access to Admin/Teacher/Assistant accounts.

---

## Token Claim Schema (Stateless JWT)

The signed temporary JWT token issued after successful profile validation contains the following claims:

| Claim Type | Value | Purpose |
|------------|-------|---------|
| `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier` | `{UserId}` (GUID) | Identifies which student is authorized to reset their password |
| `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role` | `"PasswordReset"` | Restricts the token usage to the password reset endpoint only |
| `exp` | Unix Timestamp (UtcNow + 10 minutes) | Enforces the 10-minute validity limit |
