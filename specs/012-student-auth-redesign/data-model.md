# Data Model: Student Auth Redesign

## 1. Domain Entities

*Note: The physical database schema does not change drastically. The `User` and `StudentProfile` entities still map one-to-one. The change is in how they are constructed during the onboarding process.*

### `User` (Identity)
- ID (Guid)
- FullName (String)
- PhoneNumber (String)
- PasswordHash (String)
- Role [Student]

### `StudentProfile` (Academic Details)
- UserId (Guid) -> Foreign Key
- ParentPhone (String)
- GradeId (Guid) -> References Grades
- TrackId (Guid?) -> References Tracks (Optional)
- School (String?)
- Governorate (String?)
- City (String?)

## 2. API DTOs (Data Transfer Objects)

### `RegisterStudentRequest`
- FullName (string, required)
- PhoneNumber (string, required, Regex `^01[0125][0-9]{8}$`)
- Password (string, required)
- ParentPhone (string, required, Regex `^01[0125][0-9]{8}$`)
- GradeId (Guid, required)
- TrackId (Guid?, optional)
- School (string, optional)
- Governorate (string, optional)
- City (string, optional)

## 3. Validation Rules
- `PhoneNumber` must be unique across all users.
- `ParentPhone` should format-check (same Egyptian regex).
- `GradeId` must point to a valid active grade.
