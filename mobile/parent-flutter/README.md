# مسار ولي الأمر — Flutter

The shared Android/iOS implementation lives here. Kotlin and Swift in this project only bridge legacy encrypted profile storage and the existing FCM (Android) / APNs (iOS) delivery contracts. The previous native projects remain available for upgrade verification and private signing configuration; new UI work belongs in Flutter.

## Run and verify

Use Flutter 3.41.0 / Dart 3.11 or a compatible SDK. Commit `pubspec.lock` for reproducible dependency resolution.

```sh
flutter pub get
flutter analyze
flutter test
flutter run
```

The application connects to `https://api.massar-academy.net/`. Local backend testing can use `--dart-define=PARENT_API_URL=https://your-test-api.example/`; this is a build-time endpoint, never a user setting. No sample responses or student tokens are compiled into the app. Tests replace the HTTP/device boundaries with fixtures.

[Design system](DESIGN.md) documents the reusable UI. The flow includes splash, welcome, code verification, student confirmation, dashboard, lessons/detail, exams/detail/error review, homework/detail, courses/terms/teachers, balance/history, notifications/read state, appearance, linked students and connection errors. Permission requests open the native device prompt from More; store-required updates use the existing app-config endpoint.

## Upgrade identity and local data

- Android application ID: `com.massar.parent` (unchanged).
- iOS bundle ID: `net.massaracademy.parent` (unchanged); the existing project team is retained.
- Candidate version: `3.0.0+16`. Confirm the latest uploaded build numbers in both store consoles before release; this repository cannot establish the store's current maximum.
- First Flutter launch imports Android `parent_secure_prefs` (`linked_students`, `active_student_id`) through the old AndroidX encryption API, or iOS Keychain service `com.nadergorge.parent` / account `NaderGorgeParentProfiles` plus its active-student default.
- Import persists one Flutter secure-storage envelope. Native originals are not deleted. A saved empty envelope prevents removed students from being imported again. Migration errors are shown and do not erase the old store.
- Current-session academic data is retained on network failure, cleared on authorization failure and when changing students. It is not persisted as an offline database.
- Selecting a student invalidates older in-flight responses. A late response cannot overwrite the new student's screen.

## Native builds

For Android, copy the existing **local, ignored** `../parent-android/app/google-services.json` into `android/app/google-services.json`. Use the Firebase Android app registered for `com.massar.parent`. Do not commit that local configuration or signing files.

Release signing reads the existing `../parent-android/key.properties` and resolves its `storeFile` against the old Android app directory. Debug builds use debug signing. There is no release fallback to the debug key. Preserve the existing Google Play upload-key/Play App Signing setup.

```sh
flutter build apk --debug
flutter build appbundle --release
flutter build ipa --release
```

Android requires Java 17, an Android SDK and accepted licenses. iOS requires full Xcode, CocoaPods, the existing Apple team/provisioning, Keychain access and Push Notifications entitlement. Debug APNs uses development, Release uses production. iOS registers the APNs token with the backend, not an FCM token.

## Release verification still required

Install over the actual published native versions using store-compatible signing. Verify that multiple linked students and the active student survive the update on both platforms. Test notification delivery, taps, permission denial, token rotation and expired linking tokens on physical devices. Complete iOS archive signing and TestFlight / Play internal testing before store rollout. Flutter widget tests cannot prove native Keychain/Android encryption compatibility or actual push delivery.

No store rollout, backend migration or production deployment is performed by this project change.

## Verification on 2026-09-24

Flutter analysis and twelve tests pass. The debug Android APK builds successfully with Java 17; it is for fresh debug installs and is not signed for upgrading the store application. Swift source parses, but this machine has Command Line Tools rather than full Xcode, so no iOS archive or physical-device migration/push verification has been completed.
