# Mobile applications

- `parent-flutter/`: shared Android/iOS parent application and reusable Massar design system. New parent UI and feature development belongs here. See its README for upgrade identities, legacy-profile import and store verification.
- `parent-android/`, `parent-ios/`: previous native parent implementations retained for upgrade comparison and existing private release configuration. Do not add parallel UI features here while adopting Flutter.
- `payment-listener-android/`: separate payment listener application; unaffected by the parent migration.
