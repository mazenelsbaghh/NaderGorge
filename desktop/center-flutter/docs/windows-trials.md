# Private Windows host/client trials

The `github-draft-release-assets-v1` delivery workflow can build in the reviewed public repository when the owner explicitly authorizes temporary public builds. It uploads each ZIP and checksum to an unpublished draft release, checks that release and asset metadata return 404 without authentication, and retains the source/run/attempt/role bindings. Draft lookup uses authenticated release listing; GitHub's by-tag endpoint returns published releases. The draft is never published. Older sealed workflows retain their private-repository requirement.

Restore the build repository to private at the owner's authorized completion point and verify the flag. Existing historical attachments can become public when their repository becomes public; a clean source export does not hide old releases. Do not alter historical releases without authorization. Trial delivery remains separate from publishing the protected app-update manifests.

This lane builds two trial ZIPs from one reviewed source commit without updating production or app-update manifests. Version `1.4.6+27` supports a host hotspot before login, a disconnected secondary exam without staff login or per-class roster preparation, automatic academic-catalog caching, manual student identity entry, and one collective host import with missing-attendance XLSX export. Actual per-student maxima are preserved, including paper/electronic alternatives. It preserves the homework hotspot, collective Excel import, five global WhatsApp requests and the existing reviewed private grade repair. This trial retains the dedicated WhatsApp workspace and its saved variable bindings. It reuses the actual native reception navigation in the exam workspace, replaces stacked navigation with three room stages, keeps class metadata tied to the saved destination, and protects editor/manual-grading drafts during native navigation. It adds a collected monthly-receipt allocation to online from student history, including used packages and closed cash sessions. The original receipt, saved cash closing, payment checks, attendance and purchased-session credit remain unchanged. Immutable allocation records retain amount, reason, actor and time; full receipt refunds are blocked after an online allocation. Reports distinguish center and online portions. No private repair input is changed. Physical network coverage, hotspot support and real account delivery remain separate from software checks. Student Wi-Fi names are runtime configuration; packaging does not rename them.

Use the canonical working directory. Do not create a development checkout or copy. `windows_trial.py` uses an explicit source allowlist and a temporary Git index under `artifacts/private`; the normal working index is preserved. The trial source branch must begin with `codex/trial-`. Production publication scripts remain separate and unchanged.

## Prepare after source review

Review relevant changes against the last verified desktop source, including any later desktop/exam fixes in shared production. Do not import unrelated platform edits. The source parent for this trial is `626db1c8b355e5ba6539a4c0655e0395e216697e`; the target branch is `codex/trial-online-payment-allocation-20261015`. Readiness and physical-device evidence must remain separate.

Run from the repository root:

```sh
python3 desktop/center-flutter/scripts/windows_trial.py scan \
  --parent 626db1c8b355e5ba6539a4c0655e0395e216697e \
  --output artifacts/private/windows-trial-1.4.6
```

Review the generated `source-paths.json`. Scanning includes new unignored files under the desktop and exam source roots, plus Go, Dart, hotspot C# source and portal-proxy HTML templates even when local Git excludes hide a whole project. Private installer JSON, generated build directories and the device-specific `recover-windows-exam-setup.ps1` incident helper are excluded. Missing source files, private paths and secret-like source content stop the operation. Preparation and dispatch reject new or changed build sources omitted from the reviewed allowlist.

```sh
python3 desktop/center-flutter/scripts/windows_trial.py prepare \
  --parent 626db1c8b355e5ba6539a4c0655e0395e216697e \
  --version 1.4.6+27 \
  --branch codex/trial-online-payment-allocation-20261015 \
  --scope artifacts/private/windows-trial-1.4.6/source-paths.json \
  --baseline artifacts/private/windows-release-1.3.3 \
  --private-overlay 'artifacts/private/<reviewed-overlay>' \
  --output artifacts/private/windows-trial-1.4.6
```

The baseline must have a published/download-verified host ZIP, private-input manifest and matching private GitHub asset. Its twelve historical private inputs, including the clean first-install seed, are immutable. The client remains restricted to five public assets and contains no student seed or runtime database. A private trial is not permission to make the host bundle public.

For the current source, add `--private-overlay artifacts/private/<reviewed-overlay>` to preparation. Historical baseline verification still runs first. The overlay ZIP must contain exactly seventeen root JSON files: all twelve historical files unchanged, plus `grade_results_repair_20261015.json`, `gec_month2_lesson3_attendance_20261031.json`, `jaber_month2_lesson3_attendance_20261031.json`, `shobra_month2_lesson3_attendance_20261031.json`, and `haram_month2_lesson3_attendance_20261031.json`. The existing first-install operational-history guard remains active; a full database backup is not an installation seed. The repair order is GEC, Jaber, Shobra (the owner-confirmed Raay group), then Haram (the owner-confirmed Alfa Cairo group); each requires the preceding imported session and preserves a durable application marker.

The overlay folder contains `private-packaging-assets.zip`, `private-packaging-manifest.json`, `private-assets-upload.json`, and `grade-repair-review.json`. The packaging manifest requires `schemaVersion: 1`, `privateOnly: true`, the ZIP `sha256`, seventeen per-file digests in `files`, `baselineSha256`, `reviewManifest: "grade-repair-review.json"`, and `reviewManifestSha256`. Upload evidence requires the positive GitHub asset `id` and matching ZIP `sha256`. The grade review requires `status: "verified"`, `ownerAuthorized: true`, `sourceBackupSha256`, the unchanged baseline `installationSeedSha256`, and its `repairSha256`. Its nested `attendanceRepair` (GEC), `jaberAttendanceRepair`, `shobraAttendanceRepair`, and `haramAttendanceRepair` objects each require `status: "verified"`, `ownerAuthorized: true`, a `repairSha256` matching the corresponding private asset, and SHA-256 `sourceSha256` and `sourceBackupSha256` values. Source preparation records these digests with the uploaded asset identity. Unreviewed repairs, changed historical inputs, symlinked/outside folders and unexpected ZIP entries stop preparation. Prepare the overlay only after actual repair, conflict and repeat-application verification. A locally prepared overlay has no upload evidence until protected upload is complete. These evidence fields bind the reviewed inputs; they do not replace the data review itself.

After relevant tests and source review pass, freeze source and seal it:

```sh
python3 desktop/center-flutter/scripts/windows_trial.py seal \
  --verified --output artifacts/private/windows-trial-1.4.6
```

This creates Git objects only. Source changed after the reviewed inventory blocks sealing; source changed after sealing blocks dispatch. Review the candidate paths, commit and privacy scan before dispatch.

If a CI failure requires corrected source, preserve the failed run and its evidence. Use a fresh private artifact directory and a new `codex/trial-` branch for the corrected candidate; never overwrite or force-push the earlier candidate.

## Native build and download

```sh
python3 desktop/center-flutter/scripts/windows_trial.py dispatch \
  --output artifacts/private/windows-trial-1.4.6
python3 desktop/center-flutter/scripts/windows_trial.py status \
  --output artifacts/private/windows-trial-1.4.6
python3 desktop/center-flutter/scripts/windows_trial.py download \
  --output artifacts/private/windows-trial-1.4.6
```

Dispatch requires the existing GitHub credential helper and network access. Credentials stay inside the process and are not written to evidence. Only the named trial branch is pushed to the reviewed `mazenelsbaghh/massar-center-desktop` repository. Public builds require the sealed draft-delivery workflow described above. An existing different branch commit is rejected. Dispatch intent is saved before the HTTP request: if its result is uncertain, inspect status instead of retrying blindly. A changed run attempt requires review rather than automatically choosing another build.

The existing Windows workflow uses `windows-2022`, Flutter `3.41.0`, Go `1.27.1`, Node `22` and .NET `8.0.x`. Both host and client run analysis and mandatory native checks, including Windows credential protection, the Omada profile DPAPI test, staff-login UI tests, `exam_login_network_test.dart`, `omada_setup_panel_test.dart`, complete Windows exam Go tests and packaged Arabic PDF/ZIP checks. Full cross-platform Flutter checks remain a pre-dispatch responsibility when CI skips that broader suite.

Download requires both roles to succeed in the same run attempt. It verifies source, version, role, AOT, native binaries, historical host inputs and client privacy. Downloaded CI wrappers stay in memory; only the final ZIPs are saved, with disk-space checks and a checksum read-back. Existing different output files are preserved and block overwrite.

`verified-trial.json` records the two delivery files and checksums. Share these exact files through an authorized private destination, verifying uploaded names and sizes. Do not call the production publisher or replace support/update manifests for a trial. Keep `physicalWindowsVerified: false` until the owner tests the actual host, secondary, access point and student phones. Windows must have WebView2 available before the offline trial.

If an older installation still reports the same unfinished-setup lock, review that device's current state before using the previously reviewed recovery tool, with the applications closed. Do not ship its device-specific pins as a generic helper, delete a lock blindly, or include that recovery operation in normal startup.
