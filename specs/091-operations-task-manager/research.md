# Technical Decisions Log: Operations Task Manager

This document outlines key engineering choices made for the Operations Task Manager.

## 1. Overdue Task Detection Strategy

- **Decision**: Evaluate the overdue status dynamically upon querying rather than utilizing database triggers or cron jobs.
- **Rationale**: Evaluating `DueDate < DateTime.UtcNow && Status != TaskStatus.Completed` during reads removes the need for background state synchronizers and database polling triggers, keeping code stateless and DRY.
- **Alternatives Considered**: 
  - *Hangfire/Quartz scheduling job*: Introduces runtime overhead and queue complexity; overkill for simple metadata checks.
  - *DB Trigger*: Restricts logic to SQL layer, complicating testing and migration.

## 2. Comments & Attachments Storage

- **Decision**: Comments will store simple text content. Any "attachment" will be modeled as an optional URL string in the comment schema.
- **Rationale**: Leveraging text-based links for attachments avoids binary file upload complexity (S3, bucket settings) for the operational MVP. Users can link files hosted on Google Drive or on-platform documents.
- **Alternatives Considered**: 
  - *Multi-part file uploads*: Adds S3 bucket integrations, IAM credentials, and file size validation checks which increases implementation cost.

## 3. Real-time Notifications Hook

- **Decision**: Use state triggers, API polling, or database-logged notifications fetched upon dashboard refresh rather than persistent WebSockets/SignalR.
- **Rationale**: Simple on-platform notification alerts are sufficient for the daily operations checklist and keep system connections low-overhead.
