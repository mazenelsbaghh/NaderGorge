# Database Schema & Data Models: Multi-Teacher Multi-Subject Architecture and Teacher Isolation

This document outlines the PostgreSQL database models for the new entities and modified relationships.

## 1. Subject Entity

Maps to database table `Subjects`:

| Column Name | Type | Constraints | Description |
|-------------|------|-------------|-------------|
| `Id` | `UUID` | `PRIMARY KEY` | Unique identifier. |
| `Name` | `VARCHAR(200)` | `NOT NULL` | Name of the subject (e.g., Mathematics). |
| `NormalizedName` | `VARCHAR(200)` | `NOT NULL` | Normalized lowercase name. |
| `Description` | `TEXT` | `NOT NULL` | Subject description. |
| `CreatedAt` | `TIMESTAMP` | `NOT NULL` | Creation timestamp. |

## 2. TeacherProfile Entity

Maps to database table `TeacherProfiles`:

| Column Name | Type | Constraints | Description |
|-------------|------|-------------|-------------|
| `Id` | `UUID` | `PRIMARY KEY` | Unique identifier. |
| `UserId` | `UUID` | `FOREIGN KEY` references `Users(Id)` | Link to the User account. |
| `Bio` | `TEXT` | `NOT NULL` | Biography / background info. |
| `Specialization` | `VARCHAR(200)` | `NOT NULL` | Core teaching specialization. |
| `CommissionRate` | `DECIMAL(18,2)` | `NOT NULL` | Commission percentage. |
| `ProfileImageUrl` | `VARCHAR(1000)` | `NULL` | Link to profile avatar. |
| `ContactInfo` | `VARCHAR(500)` | `NOT NULL` | Phone, email or other contact info. |
| `CreatedAt` | `TIMESTAMP` | `NOT NULL` | Creation timestamp. |

## 3. TeacherSubject Entity (Many-to-Many Join Table)

Maps to database table `TeacherSubjects`:

| Column Name | Type | Constraints | Description |
|-------------|------|-------------|-------------|
| `TeacherId` | `UUID` | `FOREIGN KEY` references `TeacherProfiles(Id)` | Link to the TeacherProfile. |
| `SubjectId` | `UUID` | `FOREIGN KEY` references `Subjects(Id)` | Link to the Subject. |

*Note: Composite Primary Key on `(TeacherId, SubjectId)`.*

## 4. Entity Modifications

The following existing tables are modified to add foreign keys:

### Program Table:
- Added `SubjectId` (`UUID`, references `Subjects(Id)`). Starts nullable for migration, then becomes `NOT NULL`.

### Package Table:
- Added `TeacherId` (`UUID`, references `Users(Id)` or `TeacherProfiles(Id)`). We will link it to `Users(Id)` for convenience (since `User` represents the identity), or `TeacherProfiles(Id)`. Let's link it to `TeacherProfiles(Id)` to enforce profile presence.

### CodeGroup Table:
- Added `TeacherId` (`UUID`, references `TeacherProfiles(Id)`).

### Exam Table:
- Added `CreatedByTeacherId` (`UUID`, references `TeacherProfiles(Id)`).

### QuestionBankItem Table:
- Added `SubjectId` (`UUID`, references `Subjects(Id)`).
- Added `CreatedByTeacherId` (`UUID`, references `TeacherProfiles(Id)`).

### EssaySubmission Table:
- Added `GradedByTeacherId` (`UUID`, references `TeacherProfiles(Id)`, `NULL`).
