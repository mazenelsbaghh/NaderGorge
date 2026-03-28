# API Contract: POST /api/auth/register

**Feature**: 016-registration-form-updates
**Date**: 2026-03-28

## Endpoint

`POST /api/auth/register`

**Auth**: None (public endpoint)
**Rate Limiting**: `auth` policy (already configured)

## Request Body (UPDATED)

```json
{
  "fullName": "محمد أحمد علي حسن",
  "phoneNumber": "01012345678",
  "secondaryPhone": "01112345678",
  "password": "securePass123",
  "dateOfBirth": "2008-05-15",
  "gender": "Male",
  "governorate": "القاهرة",
  "district": "مدينة نصر",
  "address": "شارع مصطفى النحاس",
  "parentPhone": "01098765432",
  "secondaryParentPhone": "01198765432",
  "isFatherAlive": true,
  "isMotherAlive": true,
  "educationStage": "Secondary",
  "gradeLevel": "FirstSecondary",
  "studyTrack": null
}
```

### Field Changes from Current

| Field | Change | Required | Validation |
|-------|--------|----------|------------|
| `studentCode` | **REMOVED** | — | No longer sent |
| `secondaryPhone` | **ADDED** | No | Egyptian phone format or empty/null |
| `district` | **ADDED** | No | Max 200 characters |
| `secondaryParentPhone` | **ADDED** | No | Egyptian phone format or empty/null |

### Validation Rules

| Field | Rule | Error |
|-------|------|-------|
| `fullName` | Non-empty, max 200, ≥ 4 words | "Full name must contain at least 4 parts" |
| `phoneNumber` | Non-empty, matches `^01[0125]\d{8}$` | "Invalid Egyptian phone number" |
| `secondaryPhone` | If provided, matches `^01[0125]\d{8}$` | "Invalid Egyptian phone number" |
| `password` | Non-empty, min 8 chars | — |
| `dateOfBirth` | Must be in the past | "Date of birth must be in the past" |
| `governorate` | Non-empty | — |
| `district` | Max 200 | "District name too long" |
| `address` | Non-empty | — |
| `parentPhone` | Non-empty, matches `^01[0125]\d{8}$` | "Invalid Egyptian parent phone number" |
| `secondaryParentPhone` | If provided, matches `^01[0125]\d{8}$` | "Invalid Egyptian parent phone number" |
| `educationStage` | Valid enum (Secondary/Baccalaureate) | — |
| `gradeLevel` | Valid enum | — |
| `studyTrack` | Required when grade requires it, null otherwise | — |

## Response (UNCHANGED)

### 201 Created

```json
{
  "success": true,
  "data": {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "message": "Registration successful. Please log in."
  }
}
```

### 400 Bad Request

```json
{
  "success": false,
  "errors": [
    {
      "propertyName": "SecondaryPhone",
      "errorMessage": "Invalid Egyptian phone number"
    }
  ]
}
```

## Breaking Changes

> **`studentCode` is no longer accepted in the request body.** This is a breaking change for any client that sends this field. However, since the backend uses model binding and FluentValidation, sending an extra `studentCode` field will be silently ignored (no error). The field is simply no longer validated or saved during registration.

## Admin List Users DTO Update

The `AdminUserListDto` will include the new fields:

```json
{
  "id": "...",
  "phoneNumber": "01012345678",
  "secondaryPhone": "01112345678",
  "status": "Active",
  "fullName": "محمد أحمد علي حسن",
  "grade": "FirstSecondary",
  "track": "N/A",
  "createdAt": "2026-03-28T00:00:00Z",
  "roles": ["Student"],
  "studentCode": "",
  "dateOfBirth": "2008-05-15",
  "gender": "Male",
  "educationStage": "Secondary",
  "isFatherAlive": true,
  "isMotherAlive": true,
  "governorate": "القاهرة",
  "district": "مدينة نصر",
  "address": "شارع مصطفى النحاس",
  "secondaryParentPhone": "01198765432"
}
```
