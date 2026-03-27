# API Contract: Registration

## POST /api/auth/register

Register a new student with all required data in a single request.

### Request Body

```json
{
  "fullName": "أحمد محمد علي حسن",
  "phoneNumber": "01012345678",
  "password": "securePassword123",
  "studentCode": "STU-2026-001",
  "dateOfBirth": "2008-05-15",
  "gender": "Male",
  "governorate": "القاهرة",
  "address": "مدينة نصر - شارع عباس العقاد",
  "parentPhone": "01098765432",
  "isFatherAlive": true,
  "isMotherAlive": true,
  "educationStage": "Secondary",
  "gradeLevel": "SecondSecondary",
  "studyTrack": "Science"
}
```

### Field Validation

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| fullName | string | Yes | Min 8 chars, must contain at least 4 space-separated parts |
| phoneNumber | string | Yes | Egyptian phone format (01xxxxxxxxx), unique |
| password | string | Yes | Min 8 chars |
| studentCode | string | Yes | Non-empty |
| dateOfBirth | date | Yes | Must be in the past, age 10-25 reasonable range |
| gender | enum | Yes | Male, Female |
| governorate | string | Yes | Non-empty |
| address | string | Yes | Non-empty |
| parentPhone | string | Yes | Egyptian phone format |
| isFatherAlive | bool | Yes | |
| isMotherAlive | bool | Yes | |
| educationStage | enum | Yes | Secondary, Baccalaureate |
| gradeLevel | enum | Yes | Must match stage |
| studyTrack | enum | Conditional | Required for SecondSecondary and SecondBaccalaureate, null otherwise |

### Conditional Validation Matrix

| educationStage | gradeLevel | studyTrack required? | Valid tracks |
|----------------|------------|---------------------|--------------|
| Secondary | FirstSecondary | No (must be null) | — |
| Secondary | SecondSecondary | Yes | Arts, Science |
| Baccalaureate | FirstBaccalaureate | No (must be null) | — |
| Baccalaureate | SecondBaccalaureate | Yes | MedicineAndLifeSciences, EngineeringAndComputerScience, Business, ArtsAndHumanities |

### Success Response (201 Created)

```json
{
  "id": "uuid",
  "token": "jwt-token",
  "refreshToken": "refresh-token",
  "user": {
    "id": "uuid",
    "fullName": "أحمد محمد علي حسن",
    "phoneNumber": "01012345678",
    "isProfileComplete": true
  }
}
```

### Error Responses

| Code | Condition | Body |
|------|-----------|------|
| 400 | Validation failed | `{ "errors": { "field": ["message"] } }` |
| 400 | Stage/grade/track mismatch | `{ "errors": { "gradeLevel": ["Grade does not match education stage"] } }` |
| 409 | Phone already registered | `{ "error": "Phone number already registered" }` |
