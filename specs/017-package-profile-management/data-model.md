# Data Model: Package Profile and Term Management

## Overview
This document defines the entities and relationships required to support the Package Profile Management feature. The primary focus is managing existing packages and organizing their hierarchical structure (Terms).

## Entities

### `Package` (Existing)
Represents the top-level educational module.
- `Id` (Guid, PK)
- `Title` (String)
- `Description` (String)
- `Price` (Decimal)
- `IsActive` (Boolean)
- `ThemeColor` (String, Optional layout feature)
- `Terms` (Collection Navigation Property)

#### Relationships:
- **1-to-Many** with `Term` (A package contains zero or more terms).

#### State Transitions:
- **Draft/Inactive** -> **Published/Active**: Controlled via IsActive boolean toggle on the settings screen.

### `Term` (Existing/Extension)
Represents a sub-container within a package.
- `Id` (Guid, PK)
- `PackageId` (Guid, FK)
- `Title` (String, e.g., "الترم الأول" - First Term)
- `Order` (Integer, determines display sequence in profile)
- `IsActive` (Boolean)

#### Relationships:
- **Many-to-1** with `Package` (A term belongs to exactly one Package).
- **1-to-Many** with `Section` (The downstream hierarchy container).

## Validation Rules
1. **Package Updates**: Price must be non-negative. Title cannot be empty.
2. **Term Addition**: A Term must always be associated with a valid `PackageId`. Its `Title` must be non-empty and uniquely named within the specific Package hierarchy (e.g., cannot add two "الترم الأول" to the same package).

## Permissions & Access Control
- **View/Edit Packages**: Requires `Admin` role.
- **Add Terms**: Requires `Admin` role.
- The UI must enforce role checks and handle unauthorized responses gracefully (JWT Bearer token).
