# Technical Research: Student Forgot Password / نسيت كلمة المرور للطالب

## Decisions & Rationale

### 1. Verification of Profile Fields
- **Decision**: Validate `PhoneNumber` (student's), `ParentPhone`, `Governorate`, and `District` on the backend.
- **Rationale**: This is a stateless, secure verification strategy. The system checks if a student user matches all four criteria.
- **Parent Phone Match Details**: The parent phone submitted can match *any* of the three possible parent phone fields in the `StudentProfile` table: `ParentPhone` (father's), `MotherPhone` (mother's), or `SecondaryParentPhone` (secondary contact). This is highly user-friendly since students might submit their father's or mother's phone.
- **Location Matching**: The governorate and district inputs are matched exactly (trimmed, case-insensitive).

### 2. Password Reset State Management
- **Decision**: Use a temporary, signed JSON Web Token (JWT) with a short 10-minute lifetime to pass authorization from Step 1 (Verification) to Step 2 (Reset).
- **Rationale**:
  - Fully stateless: No database modifications or Redis keys are required to store temporary reset state, reducing storage overhead.
  - Highly secure: The token is cryptographically signed using the application's JWT secret key, making it impossible to forge.
  - Time-limited: Standard JWT expiration ensures it automatically invalidates after 10 minutes.
  - Role-limited: The token contains the role/claim `"PasswordReset"`. Only a token with this specific role will be accepted by the reset endpoint.

### 3. JWT Expiration Customization
- **Decision**: Extend `ITokenService` and its implementation `TokenService` with an overload that accepts a custom lifetime:
  ```csharp
  string GenerateAccessToken(User user, IEnumerable<string> roles, TimeSpan lifetime);
  ```
- **Rationale**: Reuses the existing robust token generation logic (`SigningCredentials`, issuer, audience, and claim list mapping) while allowing a custom 10-minute lifetime for the reset token, avoiding code duplication.

---

## Alternatives Considered

### Alternative A: Single-request validation and reset
- *Approach*: The student submits all verification fields along with the new password in a single endpoint call.
- *Why Rejected*: Although simple, it doesn't align with the two-step wizard UI where the student should first know if their verification was successful before typing and validating a new password. It also makes API rate limiting harder to apply selectively (e.g. rate limiting verification separate from actual password updates).

### Alternative B: Storing reset codes in Redis
- *Approach*: Generate a random UUID, save it in Redis with a 10-minute TTL pointing to the User ID, and return the UUID to the client.
- *Why Rejected*: Relies on an external cache dependency for a critical authentication flow. A stateless signed JWT achieves the exact same security properties without requiring database or cache writes.
