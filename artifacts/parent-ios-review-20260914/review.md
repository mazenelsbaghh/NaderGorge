# Code review: Parent iOS 1.0 (15)

## Summary
Built and uploaded to App Store Connect on 2026-09-14 at 17:05 Cairo time. Apple accepted the upload and began processing. Build 15 is visible in App Store Connect (build ID 5a2c8f20-65a1-4506-b682-b6b614aaa077). Submission confirmed at 17:10 Cairo time: Waiting for Review. App Store Connect shows version 1.0 (15) under submission 23c7e3a9-1c20-4686-8681-10803fe23d90. Updated review notes and encryption answers were saved; the existing automatic release after approval setting was retained. This review covers the rejection, linking, dashboard refresh and notification decoding; it is not a complete security audit.

## Critical findings
- **Rejected QR action removed in current source** — Apple rejected 1.0 (14) on August 25 because Scan QR did nothing on iPhone 17 Pro Max and iPad Air 11-inch (M3). The August 27 source already removed this action, but only build 14 was present in App Store Connect before this run. Build 15 now contains that correction.
- **Inert support action — fixed** — `Views/LinkingView.swift` had `Button(action: {})`. It now opens the existing support URL, which returned HTTP 200.
- **Previous student data survives switching — fixed for dashboard refresh** — `ViewModels/DashboardViewModel.swift` now clears displayed data when the selected profile changes, persists the selection, and ignores obsolete refresh results. A delayed-response regression test passes.
- **Link navigation despite persistence failure — fixed** — `Views/LinkingView.swift` only invokes the success callback after the view model actually reports success.
- **Malformed notification payload treated as empty success — fixed** — `Services/APIService.swift` now propagates decoding failure.
- **Local XCTest replacement — corrected configuration** — `Package.swift` no longer depends on the local XCTest substitute. Eight tests execute under Apple's actual XCTest framework and pass.

## Important findings
- Physical-device interaction, fresh-install/upgrade journeys and APNs delivery were not verified in this run. Both paired iPhones were unavailable. Simulator installation and process launch succeeded on iPhone 17 Pro Max and iPad Air 11-inch (M3), iOS 26.5, but UI automation could not attach to Simulator; these are launch checks, not complete UI smoke tests.
- Existing Keychain code switches to a volatile in-memory store when signing entitlements are missing. Normal signed archive creation succeeded, but persistence across device restarts remains unverified.
- Notification mark-as-read refresh has a separate asynchronous path that is not covered by the dashboard refresh regression test; rapid profile switching during that operation remains unverified.
- Apple previously requested a physical-device video and app details. The owner supplied a video on August 24; the accompanying reply contained a placeholder instead of the tracking code. The existing Notes field does contain a working code. Revised Notes were saved with the working code, the actual verification results and the physical-device testing limitation. A redacted reference draft is provided separately.

## Nits
No cosmetic cleanup was included in this release.

## What's good
- Current production Swift APIService and models successfully decoded live app configuration, reviewer login, student details and notifications.
- Session tokens remain in the existing Keychain service; no bypass of authentication or production database change was introduced.
- Release simulator build, signed device archive and App Store upload succeeded. Uploaded app identifier is `net.massaracademy.parent`, version `1.0`, build `15`.

## Self-check coverage
- [x] Walked Section A (naming & functions)
- [x] Walked Section B (comments & formatting)
- [x] Walked Section C (SOLID)
- [x] Walked Section D (DRY/KISS/YAGNI)
- [x] Walked Section E (AI failure modes)

## Evidence
- `parent-review-tests.log`: 8 XCTest tests, zero failures.
- `parent-review-build.log`: BUILD SUCCEEDED.
- `parent-review-archive.log`: ARCHIVE SUCCEEDED.
- `parent-review-upload.log`: Upload succeeded; EXPORT SUCCEEDED.
- `source-sha256.json`: Swift source hashes for this upload.
- Archive: `/Volumes/external/XcodeData/Archives/Parent-20260914.xcarchive`.
