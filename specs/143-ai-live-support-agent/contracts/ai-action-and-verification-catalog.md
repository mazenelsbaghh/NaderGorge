# AI Action, Readable Data, Lookup, and Verification Catalog

## Publication rule

The server exposes stable keys and validation metadata. Policy publication fails for unknown, removed, unsafe, or incompatible keys. Worker receives keys and safe schemas only. Backend executes.

## Initial action keys

Reuse the supported participant-owned live-support catalog where the server can create a safe confirmation proposal:

- `student.profile.update`
- `student.password.reset` (secure participant input; secret excluded from AI context/transcript)
- `student.account.status.set`
- `student.note.add`, `student.note.delete`
- `student.device.disconnect`, `student.devices.disconnect-all`
- `student.package.cancel`
- `student.balance.adjust`
- `student.gamification.adjust`
- `student.video.override.add`, `student.watch.reset`, `student.watch.count.set`
- `student.watch-request.approve`, `student.watch-request.reject`
- `student.lesson.unlock`
- `student.crm.assign`, `student.crm.call.add`
- `student.create-and-link` (guided account creation with secure fields)

Always excluded: platform settings, role management, bulk codes, payroll/finance administration, teacher/content authoring, media operations, audit deletion, cross-account action, and any action without a participant-owned target.

All listed actions require explicit participant confirmation in AI mode regardless of staff-mode risk metadata.

## Readable data section keys

`identity.basic`, `identity.contact`, `account.status`, `education.profile`, `packages.active`, `access.grants`, `balance.summary`, `devices.summary`, `watch.summary`, `exams.summary`, `homework.summary`, `requests.summary`, `gamification.summary`, `notes.safe`, `crm.safe`, `audit.safe_recent`.

Each section has a server-side projection and redaction allowlist. Password/hash/token/payment-secret fields never exist in a section.

## Safe lookup keys

Initial candidates: `phone.full`, `student_code.full`. Admin may enable a subset. The request value must be complete. All participant responses are generic and reveal no existence/match count.

## Verification question keys

Initial server-reviewed candidates may include `profile.full_name`, `profile.birth_date`, `profile.governorate`, `profile.school_name`, `contact.parent_phone_last4`, and other non-secret stored fields only after security review.

The server defines comparison mode and eligibility. Empty, shared, stale, overly guessable, password, hash, token, device fingerprint, payment, and protected fields are prohibited. Multiple candidate matches fail closed.
