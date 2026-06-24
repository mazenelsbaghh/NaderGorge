# Quickstart & Verification Commands

This document contains commands to set up, build, and test the parent tracking system across all layers.

## 1. Backend API (C# .NET 9.0)

### Restore & Build:
```bash
dotnet restore backend/NaderGorge.sln
dotnet build backend/NaderGorge.sln --no-restore
```

### Apply Migrations:
```bash
cd backend/src/NaderGorge.Infrastructure
dotnet ef database update --startup-project ../NaderGorge.API
```

### Run Unit Tests:
```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~Parent"
```

---

## 2. Next.js Web Frontend (React 19)

### Install & Typecheck:
```bash
npm --prefix frontend install
npm --prefix frontend run typecheck
```

### Run E2E Tests:
```bash
npx --prefix frontend playwright test tests/e2e/parent-flow.spec.ts
```

---

## 3. Node.js Worker & Firebase Notifications

### Install & Run Tests:
```bash
npm --prefix worker install
npm --prefix worker test
```

---

## 4. Mobile Apps Build & Tests

### Android App (Kotlin / Jetpack Compose)
Since local machine does not have JDK installed, we execute compilation and unit tests inside a Docker container:
```bash
docker run --rm -v $(pwd)/mobile/parent-android:/app -w /app mobiledevops/android-sdk-image:34.0.0 ./gradlew testDebugUnitTest
```

### iOS App (Swift / SwiftUI)
Compile and run unit tests for the Swift Package Manager project using macOS CLI:
```bash
cd mobile/parent-ios
swift build
swift test
```

---

## 5. Unified Makefile Commands
We provide shortcut Makefile tasks to run the complete build verification:

```makefile
# Makefile targets for mobile compilation checks
build-mobile-android:
	docker run --rm -v "$(PWD)/mobile/parent-android:/app" -w /app mobiledevops/android-sdk-image:34.0.0 ./gradlew test

build-mobile-ios:
	cd mobile/parent-ios && swift build && swift test

build-mobile: build-mobile-android build-mobile-ios
```
Run `make build-mobile` to verify all mobile app compilation and unit tests.
