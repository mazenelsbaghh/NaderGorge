# Data Model: Call Center CRM

## Entities & Fields

### 1. `CrmStudentStatus.cs` (1-to-1 extension of Student User)
- **Table Name**: `CrmStudentStatuses`
- **Fields**:
  - `StudentId` (Guid, PK, FK to `Users.Id`)
  - `Status` (Enum: `CrmStatus` -> `Unassigned = 0`, `Assigned = 1`, `InProgress = 2`, `Cold = 3`, `Closed = 4`)
  - `AssignedAgentId` (Guid, Nullable FK to `Users.Id`)
  - `Priority` (Enum: `CrmPriority` -> `Low = 0`, `Medium = 1`, `High = 2`, `Critical = 3`, default is `Medium`)
  - `NextFollowUpDate` (DateTime, Nullable)
  - `LastCalledAt` (DateTime, Nullable)
  - `Notes` (String, Nullable)
- **Relationships**:
  - `Student` (Navigation property to `User`)
  - `AssignedAgent` (Navigation property to `User`)

### 2. `CrmCallLog.cs` (Audit timeline of student calls)
- **Table Name**: `CrmCallLogs`
- **Fields**:
  - `Id` (Guid, PK)
  - `StudentId` (Guid, FK to `Users.Id`)
  - `AgentId` (Guid, FK to `Users.Id`)
  - `CallDate` (DateTime, default is `UtcNow`)
  - `Outcome` (Enum: `CallOutcome` -> `Completed = 0`, `Pending = 1`, `NoAnswer = 2`, `Postponed = 3`, `Closed = 4`)
  - `Notes` (String, Nullable)
  - `NextFollowUpDate` (DateTime, Nullable)
- **Relationships**:
  - `Student` (Navigation property to `User`)
  - `Agent` (Navigation property to `User`)

---

## Mappings & Indexes

### AppDbContext Mappings
In `NaderGorge.Infrastructure/Data/AppDbContext.cs`:
1. `CrmStudentStatus`:
   - Primary key: `StudentId`
   - Configure one-to-one relationship with `User`:
     ```csharp
     builder.Entity<CrmStudentStatus>()
         .HasKey(s => s.StudentId);

     builder.Entity<CrmStudentStatus>()
         .HasOne(s => s.Student)
         .WithOne()
         .HasForeignKey<CrmStudentStatus>(s => s.StudentId)
         .OnDelete(DeleteBehavior.Cascade);

     builder.Entity<CrmStudentStatus>()
         .HasOne(s => s.AssignedAgent)
         .WithMany()
         .HasForeignKey(s => s.AssignedAgentId)
         .OnDelete(DeleteBehavior.SetNull);
     ```
   - Index on `AssignedAgentId` and `NextFollowUpDate`.

2. `CrmCallLog`:
   - Configure relationships:
     ```csharp
     builder.Entity<CrmCallLog>()
         .HasOne(l => l.Student)
         .WithMany()
         .HasForeignKey(l => l.StudentId)
         .OnDelete(DeleteBehavior.Cascade);

     builder.Entity<CrmCallLog>()
         .HasOne(l => l.Agent)
         .WithMany()
         .HasForeignKey(l => l.AgentId)
         .OnDelete(DeleteBehavior.Restrict);
     ```
   - Index on `StudentId` and `CallDate`.
