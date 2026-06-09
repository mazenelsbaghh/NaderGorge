# Database Schema & Data Models: Operations Task Manager

This document outlines the PostgreSQL database models for Phase 3.

## 1. TaskItem Entity

Maps to database table `TaskItems` containing properties:

| Column Name | Type | Constraints | Description |
|-------------|------|-------------|-------------|
| `Id` | `UUID` | `PRIMARY KEY` | Unique identifier. |
| `Title` | `VARCHAR(200)` | `NOT NULL` | Task summary header. |
| `Description` | `TEXT` | `NOT NULL` | Detailed work description. |
| `AssigneeId` | `UUID` | `FOREIGN KEY` references `Users(Id)` | The staff member assigned to complete the task. |
| `CreatedById` | `UUID` | `FOREIGN KEY` references `Users(Id)` | The manager who spawned the task. |
| `Status` | `INTEGER` | `NOT NULL` | Enum: New = 1, InProgress = 2, Review = 3, Completed = 4, Paused = 5, Overdue = 6 |
| `Priority` | `INTEGER` | `NOT NULL` | Enum: Low = 1, Medium = 2, High = 3, Critical = 4 |
| `DueDate` | `TIMESTAMP UTC` | `NULL` | Task completion deadline. |
| `CompletedAt` | `TIMESTAMP UTC` | `NULL` | The timestamp when the task was approved and closed. |
| `ApprovedById` | `UUID` | `FOREIGN KEY` references `Users(Id)`, `NULL` | The manager who approved the task completion. |

## 2. TaskComment Entity

Maps to database table `TaskComments` containing properties:

| Column Name | Type | Constraints | Description |
|-------------|------|-------------|-------------|
| `Id` | `UUID` | `PRIMARY KEY` | Unique identifier. |
| `TaskId` | `UUID` | `FOREIGN KEY` references `TaskItems(Id)` | Target task references. |
| `UserId` | `UUID` | `FOREIGN KEY` references `Users(Id)` | Comment author reference. |
| `Content` | `TEXT` | `NOT NULL` | Comment content. |
| `AttachmentUrl` | `VARCHAR(1000)` | `NULL` | Optional link or file reference. |
| `CreatedAt` | `TIMESTAMP UTC` | `NOT NULL` | Audit timestamp. |

## Relations
- `TaskItem` belongs to `User` as Assignee (1-to-many, optional).
- `TaskItem` belongs to `User` as Creator (1-to-many, mandatory).
- `TaskItem` has many `TaskComment` records (cascade delete).
