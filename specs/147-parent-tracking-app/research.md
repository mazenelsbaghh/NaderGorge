# Technical Research & Design Decisions

## 1. Parent Authentication & Authorization

### Decision
Stateless JWT tokens issued directly to the parent app on successful tracking code verification. The JWT contains:
- `ClaimTypes.Role`: `"Parent"`
- `"StudentId"`: the Guid of the linked student
- `Expires`: 1 year (to avoid frequent logouts)

### Rationale
- Zero database storage overhead for parent credentials or logins.
- Fully stateless backend validation of parent requests.
- Easy client-side storage of multiple tokens for multiple children. When parent switches students, the client app sends the corresponding token in the `Authorization: Bearer <token>` header.

### Alternatives Considered
- **Creating a `Parent` user account**: Rejected. Creating credentials for parents adds massive db bloat, requires password reset flows, registration forms, and introduces complex many-to-many relationship mapping between parents and students. Stateless JWT handles this directly using the physical device configuration.

---

## 2. Multi-Student Tracking Architecture

### Decision
Client-side token list storage (`EncryptedSharedPreferences` on Android, `Keychain` on iOS). 
- The client app maintains an array/list of linked student profiles: `[ { studentId, studentName, token } ]`.
- A user selector (dropdown or list) changes the active student profile.
- API requests always target `/api/parent/student-details` and authenticate with the token of the currently selected student.

### Rationale
- Extremely simple database model. The backend doesn't need to link multiple students to one parent record.
- If parent registers a second student, the device token is registered for the new `StudentId` as well.
- Zero server-side session tracking.

### Alternatives Considered
- **Server-side parent-child mapping**: Rejected. Requires a `ParentProfile` table, a many-to-many link table to `StudentProfile`, and a complex authentication flow. Client-side state is much simpler and highly resilient.

---

## 3. Mobile App Compilation & Verification in Host Environments

### Decision
- **Android**: Enforce Android code quality and compilation checks by running Gradle inside a standard Docker container:
  ```bash
  docker run --rm -v $(pwd)/mobile/parent-android:/app -w /app mobiledevops/android-sdk-image:34.0.0 ./gradlew test
  ```
- **iOS**: iOS app is structured as a Swift Package Manager (SPM) application. This allows compiling and testing the core business logic, view models, and API integrations using:
  ```bash
  cd mobile/parent-ios && swift build && swift test
  ```
  This runs on host macOS using command-line tools without requiring full Xcode project GUI files.

### Rationale
- Host macOS environment lacks a Java Runtime Environment (JRE/JDK). Running Gradle in a Docker container ensures the Android build is completely independent of host machine dependencies.
- Using Swift Package Manager structure allows verifying iOS compilation directly from shell using standard `swift build` and `swift test` commands, which works natively on the host's active CommandLineTools.

### Alternatives Considered
- **Requiring local Android Studio & Xcode installations**: Rejected. Would block automated verification scripts in environments that lack these GUI applications.

---

## 4. Push Notification Dispatch Queue

### Decision
Events trigger a BullMQ job payload created in C# and pushed to Redis. The Node.js worker pulls the job, retrieves parent FCM tokens for the student, and sends pushes via Firebase Admin SDK.

### Rationale
- Standardizes all notification logic in the Node worker, keeping the C# API stateless and fast.
- Retries and backoffs are handled natively by BullMQ.
