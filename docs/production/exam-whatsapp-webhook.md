# WhatsApp webhook for offline exams

The offline exam app sends reports directly through Meta Cloud API. Its public
callback is `https://api.massar-academy.net/api/exam-room/whatsapp/webhook`.
The existing `/api/live-support/whatsapp/webhook` and its platform account remain
independent.

## Configuration and deployment

The API uses four dedicated environment settings:

- `ExamWhatsAppCloud__BusinessAccountId`
- `ExamWhatsAppCloud__PhoneNumberId`
- `ExamWhatsAppCloud__AppSecret`
- `ExamWhatsAppCloud__VerifyToken`

Keep the source JSON outside the repository, mode 0600, with keys
`businessAccountId`, `phoneNumberId`, `appSecret`, and `verifyToken`.
The sending token stays in the desktop app's private configuration.

Use the strict SSH environment from the ssh-server skill. Preview
`deploy/production/scripts/sync_exam_whatsapp_env.py --source-json <private-file>`
before running the same command with `--yes`. The helper updates only the exam
settings. Activate them through the normal immutable rolling release gates.
Register the callback and verification token on the exam Meta app, then subscribe
the exam WABA to that app's `messages` events.

## Receipt handling

GET verification requires the exact exam verification token. POST requests need
the exam app's HMAC-SHA256 signature and are limited to 1 MiB. Only statuses for
the configured exam WABA and phone are recorded. Foreign accounts and inbound
message bodies do not enter the platform support inbox.

`ExamWhatsAppDeliveryEvents` stores the message identifier, status, provider
timestamp, numeric error code and receipt time. Its fingerprint key deduplicates
Meta retries across cluster nodes. Events are appended so delayed delivery
notifications cannot overwrite newer ones. Recipient numbers, message text,
media, tokens and raw provider errors are not stored in this table.

Desktop 0.4.4 shows request acceptance in its local send history. It does not poll
these cloud receipts or display actual delivered/read status. No real student
message is needed to verify the callback challenge or signature.
