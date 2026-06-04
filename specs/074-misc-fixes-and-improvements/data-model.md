# Data Model: Miscellaneous Fixes and Improvements

This document lists database schema and domain model modifications.

## 1. User Entity Modifications

Modify `NaderGorge.Domain.Entities.User` class to store the reason why the account is suspended/disabled:

```diff
 public class User : BaseEntity
 {
     public string FullName { get; set; } = string.Empty;
     public string PhoneNumber { get; set; } = string.Empty;
     public string PasswordHash { get; set; } = string.Empty;
     public bool IsActive { get; set; } = true;
     public bool IsProfileComplete { get; set; } = false;
+    public string? SuspensionReason { get; set; }
 
     // Navigation properties
     public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
```

## 2. EF Core DB Migration

Generate and apply a DB migration to add the nullable column `SuspensionReason` to the `Users` table:

```sql
ALTER TABLE "Users" ADD COLUMN "SuspensionReason" text NULL;
```
