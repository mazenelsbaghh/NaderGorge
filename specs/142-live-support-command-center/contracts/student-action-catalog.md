# Student Action Catalog Contract

## Invariants

Every action handler MUST:

1. Verify live support is enabled.
2. Verify caller is current conversation owner and currently checked in/connected, or an admin explicitly intervening.
3. Verify conversation is non-terminal and currently linked to the target student.
4. Validate the typed payload through FluentValidation and existing business rules.
5. Require a client idempotency UUID and reject reuse with a different payload hash.
6. Require a confirmation version derived from action key, student ID, conversation version, and current relevant state.
7. Reuse existing Application business logic; never duplicate financial, access, watch, or academic invariants.
8. Write `LiveSupportActionExecution`, `LiveSupportEvent`, central `AuditLog`, and outbox notification with safe redacted metadata.
9. Return stable refreshed section keys so the frontend reloads only affected context.

## Catalog metadata

```ts
type StudentActionDefinition = {
  key: StudentActionKey;
  category: 'Identity' | 'Account' | 'Devices' | 'Packages' | 'Balance' |
    'Watch' | 'Academic' | 'Gamification' | 'CRM' | 'Notes';
  labelAr: string;
  danger: 'low' | 'medium' | 'high' | 'financial';
  confirmationRequired: true;
  reasonRequired: boolean;
  enabled: boolean;
  disabledReason?: string;
  confirmationVersion: string;
  payloadSchemaVersion: 1;
};
```

## Initial stable action keys

| Key | Payload V1 | Existing rule source | Danger | Refresh sections |
|---|---|---|---|---|
| `student.profile.update` | Editable profile fields and expected profile version | `UpdateStudentProfileCommand` | medium | `identity,academic,family` |
| `student.password.reset` | `newPassword` (never logged) | `AdminResetPasswordCommand` | high | `audit` |
| `student.account.status.set` | `isActive, reason` | `ToggleStudentSystemAccessCommand` | high | `account,audit` |
| `student.note.add` | `content,isPinned` | `AddStudentNoteCommand` | low | `notes,audit` |
| `student.note.delete` | `noteId,reason` | `DeleteStudentNoteCommand` | medium | `notes,audit` |
| `student.device.disconnect` | `deviceId,reason` | `DisconnectStudentDeviceCommand` | high | `devices,audit` |
| `student.devices.disconnect-all` | `reason` | `DisconnectStudentDeviceCommand` | high | `devices,audit` |
| `student.package.cancel` | `accessGrantId,refundBalance,reason` | `CancelPackageGrantCommand` | financial | `packages,balance,audit` |
| `student.balance.adjust` | `amount,reason` | `AdjustBalanceCommand` | financial | `balance,audit` |
| `student.gamification.adjust` | `points,reason` | `AdjustGamificationPointsCommand` | medium | `gamification,audit` |
| `student.video.override.add` | `videoId,addedViews,reason` | `OverrideVideoLimitCommand` | high | `watch,overrides,audit` |
| `student.watch.reset` | `lessonVideoId,reason` | `ResetWatchLimitCommand` | high | `watch,audit` |
| `student.watch.count.set` | `lessonVideoId,newWatchCount,reason` | `SetWatchCountCommand` | high | `watch,audit` |
| `student.watch-request.approve` | `requestId,reason?` | `ApproveWatchRequestCommand` | high | `watch,requests,audit` |
| `student.watch-request.reject` | `requestId,reason` | `RejectWatchRequestCommand` | medium | `requests,audit` |
| `student.lesson.unlock` | `lessonId,reason` | `ManualUnlockCommand` | high | `academic,audit` |
| `student.crm.assign` | `assignedAgentId?,priority,notes?` | `AssignStudentToAgentCommand` | medium | `crm,audit` |
| `student.crm.call.add` | existing call-log fields | `AddCrmCallLogCommand` | low | `crm,audit` |
| `student.create-and-link` | existing student creation request plus package IDs | `AdminCreateUserCommand`, then link command | high | `all` |

## Explicit exclusions

These are not actions “on the linked student” and MUST NOT appear in this catalog: global platform settings, role management, content authoring, bulk code generation, teacher finance/payroll, Bunny/AI operations, social/media planning, and deletion of audit/support evidence.

## Confirmation response

Before execution, the UI obtains or derives:

```json
{
  "actionKey": "student.balance.adjust",
  "targetStudent": { "id": "...", "displayName": "...", "maskedPhone": "010***1234" },
  "summaryAr": "إضافة 100 جنيه إلى الرصيد",
  "impactAr": "الرصيد الحالي 20 جنيه، الرصيد بعد التنفيذ 120 جنيه",
  "danger": "financial",
  "confirmationVersion": "opaque-version"
}
```

If relevant state changes before execution, the server returns `409 CONFIRMATION_STALE`; UI refreshes context and requires confirmation again.

## Audit redaction

- Password payload: record only `{ passwordChanged: true }`.
- Tokens/codes: never record plaintext; record masked identifiers.
- Profile: allowlisted field names and old/new safe values.
- Balance/package: record amount, reason, previous/resulting balance, grant ID and refund flag.
- Device: record device ID and safe browser/OS label, not fingerprint secret.
- IP/correlation: use existing central audit conventions.

## Parity gate

A test enumerates registered server action keys and the frontend `studentActionDefinitions` keys. The sets must match exactly. Each key must have one validator, one handler, one catalog definition, one confirmation rendering path, and happy/permission/validation/idempotency tests.
