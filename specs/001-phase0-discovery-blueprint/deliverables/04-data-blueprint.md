# 04 — Data Blueprint

## Student Data Fields
The platform requires specific data to support physical center operations and close follow-up:
- **Full Name:** Essential for identification in physical centers and calling parents.
- **Phone Number:** Used as the primary login identifier (and WhatsApp contact).
- **Parent Phone Number:** Mandatory secondary contact for follow-up and attendance reporting.
- **Grade (Academic Year):** Determines which content is visible (Secondary 1, 2, 3, or Preparatory 3).
- **Study Track:** For Secondary 2/3 (e.g., Science, Math, Literature).
- **Governorate:** Geographical tracking for potential center expansion.
- **City/District:** Granular location data.
- **School Name:** Needed for demographic tracking and local marketing.

## Engagement Data
The system aggressively tracks engagement to power the "At-Risk" identification:
- **Watch Time:** Precisely tracks video consumption (percentage watched, skip attempts, replays).
- **Lesson Completion:** Boolean state indicating if the student finished all mandatory lesson components.
- **Homework Completion:** Tracks submission, score out of total, and time taken.
- **Exam Performance:** Records attempts, scores, passing status, and specific weak points.
- **Inactivity:** Measures days since last login or last video watch.

## History Data
- **Package History:** An audit log of all packages the student has ever accessed.
- **Code History:** A ledger of every code the student has activated, including the date, time, and specific batch the code came from.
- **Activation Logs:** Detailed records mapping codes to specific `StudentAccessGrant` entries.

## Parent Data
- **Contact Info:** Parent's primary phone number (often used for WhatsApp notifications).
- **Linked Student(s):** Parents can be linked to multiple student accounts (e.g., siblings in different grades). Parents currently do not have a dedicated portal in Phase 1, but this relationship is required in the data model for SMS/WhatsApp routing from assistants.
